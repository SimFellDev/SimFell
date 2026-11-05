using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static FellowshipSim.Engine.Replay.RawFightValue;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Turns a raw fight (every log event of one fight, see <see cref="FellowshipLogsReports.DownloadFightAsync"/>) into a
/// hero-agnostic replay script for one player:
/// <list type="bullet">
/// <item><c>commands</c>: what the engine is told to do, in time order (begin_cast, complete_cast, cancel_cast, cast,
/// stop_channel, enemy_arise, enemy_demise, enemy_health, player_death, player_revive, and external_aura: buffs and
/// debuffs other actors put on the player or on enemies, which the engine cannot produce itself).</item>
/// <item><c>observations</c>: what actually happened, used only to check the engine (the player's damage to enemies,
/// the player's own auras, resource snapshots, channel markers).</item>
/// </list>
/// Abilities are identified by their log IDs; nothing here is specific to a hero.
/// <para>
/// The script is built in one pass over the raw fight's UTF-8 bytes: each event is decoded once (<see cref="RawEvent"/>),
/// and the script is written as UTF-8. Its memory is the script's own size plus a fixed amount.
/// </para>
/// </summary>
public static class ReplayScriptBuilder
{
    /// <summary>
    /// Combat log hit types (FellowshipAnalyzer HitType): 0 a miss, 1 a normal hit, 2 a critical strike, 7 a dodge, 22 a
    /// grievous critical strike (crit chance beyond 100%). A miss and a dodge deal no damage.
    /// </summary>
    public const int HitTypeMiss = 0;
    public const int HitTypeDodge = 7;
    public const int HitTypeCrit = 2;
    public const int HitTypeGrievousCrit = 22;

    public const string Schema = FellowshipSim.Engine.Replay.ReplayScript.SchemaVersion;

    /// <summary>Smallest change in an enemy's health fraction between two health samples.</summary>
    private const double HealthSampleStep = 0.01;

    /// <summary>A cast event this soon before a begincast of the same ability is a press that did not go through (ms).</summary>
    private const double LoneCastBeforeBeginCastMs = 500;

    /// <summary>Player actions after a cast event that the lone-cast check reads.</summary>
    private const int LoneCastLookahead = 5;

    /// <summary>Longest a begincast can stay open before it is taken as abandoned (ms).</summary>
    private const double MaxCastWindowMs = 6000;

    /// <summary>An enemy last seen this long before the fight's end is demised (ms).</summary>
    private const double LastSeenBeforeEndMs = 2000;

    /// <summary>The players of a raw fight's UTF-8 JSON (actor ID, name, hero), from its head alone.</summary>
    public static IReadOnlyList<ReportPlayer> Players(ReadOnlySpan<byte> rawFight) => Players(RawFightHead.Read(rawFight));

    private static List<ReportPlayer> Players(RawFightHead raw)
    {
        var friendly = raw["fight"]["friendlyPlayers"].Items.Select(x => (int)Num(x)).ToHashSet();
        return raw["master_data"]["actors"].Items
            .Where(a => Str(a["type"]) == "Player" && friendly.Contains((int)Num(a["id"])))
            .Select(a => new ReportPlayer((int)Num(a["id"]), a["name"].IsNull ? "?" : Str(a["name"]), Str(a["subType"])))
            .ToList();
    }

    /// <summary>
    /// The replay script of player <paramref name="playerId"/> in a raw fight's UTF-8 JSON, as UTF-8 JSON. Throws
    /// <see cref="ArgumentException"/> for a raw fight without the player's events, and for events out of time order.
    /// </summary>
    public static byte[] Build(ReadOnlyMemory<byte> rawFight, int playerId) => new Builder(rawFight, playerId).Build();

    /// <summary>
    /// <see cref="Build(ReadOnlyMemory{byte}, int)"/> for the raw fight in <paramref name="rawFight"/>, which the build
    /// empties: once the script is built, nothing keeps the raw fight.
    /// </summary>
    public static byte[] Build(RawFightHolder rawFight, int playerId) => Build(rawFight.Take(), playerId);

    /// <summary>Order of commands at the same moment.</summary>
    private enum KindOrder
    {
        EnemyArise = 0, EnemyHealth = 1, ExternalAura = 1, PlayerRevive = 1, CancelCast = 2, StopChannel = 3,
        BeginCast = 4, CompleteCast = 4, Cast = 4, PlayerDeath = 8, EnemyDemise = 9,
    }

    /// <summary>
    /// The builder's sections, in the order they add commands. Commands at one time and of one order keep their section's
    /// order, then the order the section added them in.
    /// </summary>
    private enum Section { EnemyLifetimes, EnemyHealth, PlayerDeaths, PlayerActions, Auras }

    /// <summary>A command's place in the script, and its UTF-8 JSON in the commands buffer.</summary>
    private readonly record struct CommandEntry(double T, KindOrder Order, Section Section, int Sequence, long Start, int Length);

    /// <summary>An enemy's lifetime: when it was first and last seen, its death, its largest maximum health, and its name.</summary>
    private sealed class EnemyLifetime(double firstSeen)
    {
        public double FirstSeen = firstSeen, LastSeen = firstSeen, MaxHp;
        public double? DeathAt;
    }

    private sealed class Builder
    {
        private readonly ReadOnlyMemory<byte> _raw;
        private readonly int _playerId;
        private readonly RawFightHead _head;
        private double _f0, _fightEnd;

        // The actors by ID, and each enemy's encoded name.
        private readonly Dictionary<int, RawFightValue> _actors = new();
        private readonly Dictionary<int, JsonEncodedText> _enemyNames = new();
        private readonly Dictionary<long, JsonEncodedText> _abilityNames = new();

        // Commands, written as they are found and sorted at the end.
        private readonly ScriptBuffer _commandBytes = new(1 << 16);
        private readonly List<CommandEntry> _commands = new();
        private readonly Utf8JsonWriter _command;
        private int _sequence;

        // Observations, written as they are found.
        private readonly ScriptBuffer _damageBytes = new(1 << 16), _auraBytes = new(1 << 16), _resourceBytes = new(1 << 16);
        private readonly ScriptBuffer _channelBytes = new(), _ignoredBytes = new();
        private readonly Utf8JsonWriter _damage, _auras, _resources, _channels, _ignored;

        // Enemy lifetimes, in the order enemies were first seen.
        private readonly List<(int Id, int Instance)> _enemyOrder = new();
        private readonly Dictionary<(int, int), EnemyLifetime> _lifetimes = new();

        // Enemy health: each enemy's last sampled fraction.
        private readonly Dictionary<(int, int), double> _lastFraction = new();

        // Player deaths.
        private bool _dead;

        // Player actions: begincast, cast, beginchannel and endchannel events of the player.
        private readonly List<RawEvent> _playerActions = new();

        // The combatantinfo events, and the first dungeonstart's affixes.
        private readonly List<JsonDocument> _combatantInfos = new();
        private bool _dungeonStarted;
        private RawSlice _affixes;

        // A resource snapshot's resources by type: the first position of a type, its last values.
        private readonly List<(int Type, RawSlice Amount, RawSlice Max)> _snapshot = new();

        public Builder(ReadOnlyMemory<byte> raw, int playerId)
        {
            _raw = raw;
            _playerId = playerId;
            _head = RawFightHead.Read(raw.Span);
            _command = new Utf8JsonWriter(_commandBytes);
            _damage = new Utf8JsonWriter(_damageBytes);
            _auras = new Utf8JsonWriter(_auraBytes);
            _resources = new Utf8JsonWriter(_resourceBytes);
            _channels = new Utf8JsonWriter(_channelBytes);
            _ignored = new Utf8JsonWriter(_ignoredBytes);
        }

        private ReadOnlySpan<byte> Raw => _raw.Span;

        private double Rel(double ts) => Math.Round((ts - _f0) / 1000.0, 3);

        private bool IsEnemy(int? id) => id is { } i && _enemyNames.ContainsKey(i);

        private JsonEncodedText AbilityName(long id) =>
            _abilityNames.TryGetValue(id, out var n) ? n : JsonEncodedText.Encode($"ability_{id}");

        public byte[] Build()
        {
            var fight = _head["fight"];
            if (fight.Element.ValueKind != JsonValueKind.Object) throw new ArgumentException("The raw fight has no fight.");
            _f0 = Num(fight["startTime"]);
            _fightEnd = Num(fight["endTime"]);
            if (!_head["master_data"]["actors"].IsArray) throw new ArgumentException("The raw fight has no actors.");
            foreach (var a in _head["master_data"]["actors"].Items) _actors[(int)Num(a["id"])] = a;
            foreach (var (id, a) in _actors)
            {
                if (Str(a["type"]) == "NPC" && Str(a["name"]) != "Environment") _enemyNames[id] = JsonEncodedText.Encode(Str(a["name"]));
            }
            foreach (var a in _head["master_data"]["abilities"].Items) _abilityNames[(long)Num(a["gameID"])] = JsonEncodedText.Encode(Str(a["name"]));
            if (_head.EventsStart < 0) throw new ArgumentException("The raw fight has no events.");

            _damage.WriteStartArray();
            _auras.WriteStartArray();
            _resources.WriteStartArray();
            _channels.WriteStartArray();
            _ignored.WriteStartArray();
            ReadEvents();

            // The character comes from the player's combatantinfo event, which a fight downloaded in part can lack.
            var combatantEvent = _combatantInfos.FirstOrDefault(c => Int(new RawFightValue(c.RootElement)["sourceID"]) == _playerId)
                ?? throw new ArgumentException($"Fight {Num(fight["id"])} has no character information (combatantinfo) for player {_playerId}.");
            var combatant = (JsonObject)JsonNode.Parse(combatantEvent.RootElement.GetRawText())!;

            AddEnemyLifetimes(out var enemies);
            AddPlayerActions();

            _damage.WriteEndArray();
            _auras.WriteEndArray();
            _resources.WriteEndArray();
            _channels.WriteEndArray();
            _ignored.WriteEndArray();
            foreach (var w in new[] { _damage, _auras, _resources, _channels, _ignored, _command }) w.Flush();

            var head = Head(combatant, enemies);
            return Script(head);
        }

        // ---- The events ---------------------------------------------------------------------
        // FellowshipLogs' pages are in time order and overlap at their boundary timestamp, so a duplicate event has the
        // timestamp of the one it repeats. An event equal to one before it at its timestamp is dropped.
        private void ReadEvents()
        {
            int offset = _head.EventsStart;
            var reader = new Utf8JsonReader(Raw[offset..]);
            reader.Read();
            var atTimestamp = new List<(int Start, int Length)>();
            double timestamp = double.NegativeInfinity;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType is JsonTokenType.StartArray) reader.Skip();
                if (reader.TokenType != JsonTokenType.StartObject) continue;
                var e = RawEvent.Read(ref reader, offset);
                if (e.Timestamp < timestamp) throw new ArgumentException($"The raw fight's events are out of time order at timestamp {e.Timestamp}.");
                if (e.Timestamp != timestamp)
                {
                    atTimestamp.Clear();
                    timestamp = e.Timestamp;
                }
                var text = Raw.Slice(e.Start, e.Length);
                bool seen = false;
                foreach (var (start, length) in atTimestamp)
                {
                    if (Raw.Slice(start, length).SequenceEqual(text))
                    {
                        seen = true;
                        break;
                    }
                }
                if (seen) continue;
                atTimestamp.Add((e.Start, e.Length));
                Read(in e);
            }
        }

        private void Read(in RawEvent e)
        {
            switch (e.Type)
            {
                case RawEventType.CombatantInfo:
                    _combatantInfos.Add(JsonDocument.Parse(_raw.Slice(e.Start, e.Length)));
                    break;
                case RawEventType.DungeonStart when !_dungeonStarted:
                    _dungeonStarted = true;
                    _affixes = e.Affixes;
                    break;
            }
            SeeEnemies(in e);
            SampleEnemyHealth(in e);
            FollowPlayerDeath(in e);
            if (e.SourceId == _playerId && e.Type is RawEventType.BeginCast or RawEventType.Cast or RawEventType.BeginChannel or RawEventType.EndChannel)
            {
                _playerActions.Add(e);
            }
            AddAuraDamageResources(in e);
        }

        // ---- Enemy lifetimes ---------------------------------------------------------------
        private void SeeEnemies(in RawEvent e)
        {
            See(e.SourceId, e.SourceInstance, e.Timestamp, in e.SourceResources);
            See(e.TargetId, e.TargetInstance, e.Timestamp, in e.TargetResources);
            if (e.Type == RawEventType.Death && IsEnemy(e.TargetId))
            {
                var lifetime = _lifetimes[(e.TargetId!.Value, e.TargetInstance)];
                lifetime.DeathAt ??= e.Timestamp;
            }
        }

        private void See(int? id, int instance, double ts, in RawUnitResources resources)
        {
            if (!IsEnemy(id)) return;
            var key = (id!.Value, instance);
            if (!_lifetimes.TryGetValue(key, out var lifetime))
            {
                _lifetimes[key] = lifetime = new EnemyLifetime(ts);
                _enemyOrder.Add(key);
            }
            lifetime.LastSeen = ts;
            if (resources.HasMaxHitPoints && resources.MaxHitPoints > 0) lifetime.MaxHp = Math.Max(lifetime.MaxHp, resources.MaxHitPoints);
        }

        private void AddEnemyLifetimes(out ScriptBuffer enemies)
        {
            enemies = new ScriptBuffer();
            using var w = new Utf8JsonWriter(enemies);
            w.WriteStartArray();
            foreach (var key in _enemyOrder)
            {
                var lifetime = _lifetimes[key];
                bool died = lifetime.DeathAt.HasValue;
                double endT = lifetime.DeathAt ?? lifetime.LastSeen;
                var name = _enemyNames[key.Item1];
                w.WriteStartObject();
                w.WriteNumber("id"u8, key.Item1);
                w.WriteNumber("instance"u8, key.Item2);
                w.WriteString("name"u8, name);
                w.WriteNumber("max_hp"u8, lifetime.MaxHp);
                w.WriteNumber("arise_t"u8, Rel(lifetime.FirstSeen));
                w.WriteNumber("demise_t"u8, Rel(endT));
                w.WriteString("demise_reason"u8, died ? "death"u8 : "last_seen"u8);
                w.WriteEndObject();

                var c = BeginCommand();
                c.WriteNumber("t"u8, Rel(lifetime.FirstSeen));
                c.WriteString("kind"u8, "enemy_arise"u8);
                WriteUnit(c, "enemy"u8, key.Item1, key.Item2);
                c.WriteString("name"u8, name);
                c.WriteNumber("max_hp"u8, lifetime.MaxHp);
                EndCommand(Rel(lifetime.FirstSeen), KindOrder.EnemyArise, Section.EnemyLifetimes);
                // An enemy still alive at the end of the fight is not demised.
                if (died || endT < _fightEnd - LastSeenBeforeEndMs)
                {
                    c = BeginCommand();
                    c.WriteNumber("t"u8, Rel(endT));
                    c.WriteString("kind"u8, "enemy_demise"u8);
                    WriteUnit(c, "enemy"u8, key.Item1, key.Item2);
                    c.WriteString("reason"u8, died ? "death"u8 : "last_seen"u8);
                    EndCommand(Rel(endT), KindOrder.EnemyDemise, Section.EnemyLifetimes);
                }
            }
            w.WriteEndArray();
        }

        // ---- Enemy health (from everyone's damage; health-dependent effects need it) ---------
        private void SampleEnemyHealth(in RawEvent e)
        {
            if (e.Type != RawEventType.Damage || !IsEnemy(e.TargetId)) return;
            var res = e.TargetResources;
            if (!res.HasHitPoints || !res.HasMaxHitPoints || res.MaxHitPoints == 0) return;
            double hp = res.HitPoints, maxNow = res.MaxHitPoints;
            // A killing blow logs the health below 0 as an unsigned 32-bit number (1p7AXmHfwV9CYMxR fight 30 @ 157.097:
            // 4294950962 of 726971).
            if (hp > maxNow) hp = 0;
            var key = (e.TargetId!.Value, e.TargetInstance);
            double fraction = hp / maxNow;
            if (_lastFraction.TryGetValue(key, out var last) && Math.Abs(fraction - last) < HealthSampleStep) return;
            _lastFraction[key] = fraction;
            double t = Rel(e.Timestamp);
            var c = BeginCommand();
            c.WriteNumber("t"u8, t);
            c.WriteString("kind"u8, "enemy_health"u8);
            WriteUnit(c, "enemy"u8, key.Item1, key.Item2);
            c.WriteNumber("hp"u8, hp);
            c.WritePropertyName("max_hp"u8);
            res.MaxHitPointsText.WriteTo(c, Raw);
            EndCommand(t, KindOrder.EnemyHealth, Section.EnemyHealth);
        }

        // ---- Player deaths -------------------------------------------------------------------
        // A dead player's events log their health below 0 as an unsigned 32-bit number (above their maximum). Many returns
        // log no resurrect event (a release, or a wipe): the player is back at their first event with health in range.
        private static bool Alive(in RawUnitResources r) =>
            r.HasHitPoints && r.HasMaxHitPoints && r.HitPoints > 0 && r.HitPoints <= r.MaxHitPoints;

        private void FollowPlayerDeath(in RawEvent e)
        {
            bool onPlayer = e.TargetId == _playerId, byPlayer = e.SourceId == _playerId;
            if (!_dead)
            {
                if (e.Type != RawEventType.Death || !onPlayer) return;
                _dead = true;
                AddMoment(Rel(e.Timestamp), "player_death"u8, KindOrder.PlayerDeath);
                return;
            }
            if ((e.Type == RawEventType.Resurrect && onPlayer) || (byPlayer && Alive(in e.SourceResources)) || (onPlayer && Alive(in e.TargetResources)))
            {
                _dead = false;
                AddMoment(Rel(e.Timestamp), "player_revive"u8, KindOrder.PlayerRevive);
            }
        }

        private void AddMoment(double t, ReadOnlySpan<byte> kind, KindOrder order)
        {
            var c = BeginCommand();
            c.WriteNumber("t"u8, t);
            c.WriteString("kind"u8, kind);
            EndCommand(t, order, Section.PlayerDeaths);
        }

        // ---- Player actions ------------------------------------------------------------------
        // The log has no cancel event: a begincast that never completes was cancelled. The exact moment is unknown, so it
        // is placed at the player's first action after the cast began (actions used while casting without breaking it,
        // e.g. off-GCD spells, are only known to be such once the cast completes).
        private void AddPlayerActions()
        {
            RawEvent? openCast = null;
            double? firstActionSinceOpen = null;
            void CancelOpen(double fallbackT)
            {
                double t = firstActionSinceOpen ?? fallbackT;
                var c = BeginCommand();
                WriteAction(c, openCast!.Value, t);
                c.WriteString("kind"u8, "cancel_cast"u8);
                c.WriteBoolean("inferred_time"u8, true);
                EndCommand(t, KindOrder.CancelCast, Section.PlayerActions);
            }
            void Add(in RawEvent e, double t, ReadOnlySpan<byte> kind, KindOrder order)
            {
                var c = BeginCommand();
                WriteAction(c, e, t);
                c.WriteString("kind"u8, kind);
                EndCommand(t, order, Section.PlayerActions);
            }

            for (int i = 0; i < _playerActions.Count; i++)
            {
                var e = _playerActions[i];
                long gid = (long)e.AbilityGameId;
                double t = Rel(e.Timestamp);
                switch (e.Type)
                {
                    case RawEventType.BeginCast:
                        if (openCast != null) CancelOpen(t);
                        openCast = e;
                        firstActionSinceOpen = null;
                        Add(in e, t, "begin_cast"u8, KindOrder.BeginCast);
                        break;

                    case RawEventType.Cast:
                        if (e.Fake) break; // duplicate activation marker emitted alongside begincast
                        if (openCast != null && (long)openCast.Value.AbilityGameId == gid)
                        {
                            Add(in e, t, "complete_cast"u8, KindOrder.CompleteCast);
                            openCast = null;
                            break;
                        }
                        if (openCast != null && e.Timestamp - openCast.Value.Timestamp > MaxCastWindowMs)
                        {
                            CancelOpen(t);
                            openCast = null;
                        }
                        if (openCast != null) firstActionSinceOpen ??= t;
                        // A lone cast event right before the same ability's begincast is not a real activation (the game logs
                        // it when a press does not go through; e.g. Chronoshift @ 124.882 then begincast @ 125.047).
                        if (IsLoneCast(i, gid, e.Timestamp))
                        {
                            _ignored.WriteStartObject();
                            WriteAction(_ignored, e, t);
                            _ignored.WriteString("reason"u8, "cast event right before its own begincast"u8);
                            _ignored.WriteEndObject();
                            break;
                        }
                        // Instant cast. If a cast is open, the engine decides whether this ability may be used while casting.
                        Add(in e, t, "cast"u8, KindOrder.Cast);
                        break;

                    case RawEventType.BeginChannel:
                        _channels.WriteStartObject();
                        WriteAction(_channels, e, t);
                        _channels.WriteString("kind"u8, "begin_channel"u8);
                        _channels.WriteEndObject();
                        break;

                    case RawEventType.EndChannel:
                        _channels.WriteStartObject();
                        WriteAction(_channels, e, t);
                        _channels.WriteString("kind"u8, "end_channel"u8);
                        _channels.WriteEndObject();
                        Add(in e, t, "stop_channel"u8, KindOrder.StopChannel);
                        break;
                }
            }
            if (openCast != null) CancelOpen(Rel(_fightEnd));
        }

        private bool IsLoneCast(int i, long gid, double timestamp)
        {
            for (int j = i + 1; j < _playerActions.Count && j <= i + LoneCastLookahead; j++)
            {
                var n = _playerActions[j];
                if (n.Type == RawEventType.BeginCast && (long)n.AbilityGameId == gid && n.Timestamp - timestamp <= LoneCastBeforeBeginCastMs) return true;
            }
            return false;
        }

        private void WriteAction(Utf8JsonWriter w, in RawEvent e, double t)
        {
            long gid = (long)e.AbilityGameId;
            w.WriteNumber("t"u8, t);
            w.WriteNumber("ability_id"u8, gid);
            w.WriteString("ability_name"u8, AbilityName(gid));
            WriteUnit(w, "target"u8, e.TargetId ?? -1, e.TargetInstance);
        }

        // ---- Auras, damage, resources --------------------------------------------------------
        private void AddAuraDamageResources(in RawEvent e)
        {
            int? src = e.SourceId, tgt = e.TargetId;
            if (e.IsAura)
            {
                // Buffs from others on the player, and debuffs from others on enemies: engine inputs.
                if (tgt == _playerId && src == _playerId) WriteAura(_auras, in e);
                else if (tgt == _playerId || (IsEnemy(tgt) && src != _playerId))
                {
                    double t = Rel(e.Timestamp);
                    var c = BeginCommand();
                    WriteAuraFields(c, in e);
                    c.WriteString("kind"u8, "external_aura"u8);
                    EndCommand(t, KindOrder.ExternalAura, Section.Auras);
                }
                else if (IsEnemy(tgt) && src == _playerId) WriteAura(_auras, in e);
            }
            else if (e.Type == RawEventType.Damage && src == _playerId && IsEnemy(tgt))
            {
                // Only damage to enemies: party mechanics logged as the player hurting themselves (Xavian's Stagger) are not ours.
                long gid = (long)e.AbilityGameId;
                var w = _damage;
                w.WriteStartObject();
                w.WriteNumber("t"u8, Rel(e.Timestamp));
                w.WriteNumber("ability_id"u8, gid);
                w.WriteString("ability_name"u8, AbilityName(gid));
                WriteUnit(w, "target"u8, tgt, e.TargetInstance);
                WriteOrZero(w, "amount"u8, e.Amount);
                w.WritePropertyName("unmitigated"u8);
                e.UnmitigatedAmount.WriteTo(w, Raw);
                WriteOrZero(w, "absorbed"u8, e.Absorbed);
                WriteOrZero(w, "blocked"u8, e.Blocked);
                WriteOrZero(w, "overkill"u8, e.Overkill);
                w.WriteBoolean("crit"u8, e.HitType is HitTypeCrit or HitTypeGrievousCrit);
                w.WritePropertyName("hit_type"u8);
                e.HitTypeText.WriteTo(w, Raw);
                w.WriteBoolean("tick"u8, e.Tick);
                w.WriteEndObject();
            }

            if (src == _playerId && !e.SourceResources.Resources.IsNone) WriteResources(in e);
        }

        private void WriteAura(Utf8JsonWriter w, in RawEvent e)
        {
            w.WriteStartObject();
            WriteAuraFields(w, in e);
            w.WriteEndObject();
        }

        private void WriteAuraFields(Utf8JsonWriter w, in RawEvent e)
        {
            long gid = (long)e.AbilityGameId;
            w.WriteNumber("t"u8, Rel(e.Timestamp));
            w.WritePropertyName("type"u8);
            e.TypeText.WriteString(w, Raw);
            w.WriteNumber("ability_id"u8, gid);
            w.WriteString("ability_name"u8, AbilityName(gid));
            WriteUnit(w, "source"u8, e.SourceId, e.SourceInstance);
            WriteUnit(w, "target"u8, e.TargetId, e.TargetInstance);
            w.WritePropertyName("stack"u8);
            e.Stack.WriteTo(w, Raw);
            w.WritePropertyName("duration_ms"u8);
            e.Duration.WriteTo(w, Raw);
            w.WritePropertyName("extra_ability_id"u8);
            e.ExtraAbilityGameId.WriteTo(w, Raw);
        }

        // A resource snapshot of the player's resources at one of their events, by resource type.
        private void WriteResources(in RawEvent e)
        {
            var slice = e.SourceResources.Resources;
            var reader = new Utf8JsonReader(slice.Of(Raw));
            reader.Read();
            _snapshot.Clear();
            bool any = false;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                any = true;
                if (reader.TokenType == JsonTokenType.Null) continue;
                int? type = null;
                RawSlice amount = default, max = default;
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("type"u8)) { reader.Read(); type = RawEvent.Int(ref reader); }
                        else if (reader.ValueTextEquals("amount"u8)) { reader.Read(); amount = RawSlice.Read(ref reader, slice.Start); }
                        else if (reader.ValueTextEquals("max"u8)) { reader.Read(); max = RawSlice.Read(ref reader, slice.Start); }
                        else reader.Read();
                        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.Skip();
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartArray) reader.Skip();
                int key = type ?? 0;
                int at = _snapshot.Count - 1;
                while (at >= 0 && _snapshot[at].Type != key) at--;
                if (at >= 0) _snapshot[at] = (key, amount, max);
                else _snapshot.Add((key, amount, max));
            }
            if (!any) return;

            var w = _resources;
            w.WriteStartObject();
            w.WriteNumber("t"u8, Rel(e.Timestamp));
            w.WritePropertyName("event"u8);
            e.TypeText.WriteString(w, Raw);
            w.WritePropertyName("ability_id"u8);
            e.AbilityGameIdText.WriteTo(w, Raw);
            w.WriteBoolean("activation"u8, e.Activation);
            w.WriteStartObject("resources"u8);
            Span<byte> name = stackalloc byte[16];
            foreach (var (type, amount, max) in _snapshot)
            {
                type.TryFormat(name, out int written, default, CultureInfo.CurrentCulture);
                w.WriteStartObject(name[..written]);
                w.WritePropertyName("amount"u8);
                amount.WriteTo(w, Raw);
                w.WritePropertyName("max"u8);
                max.WriteTo(w, Raw);
                w.WriteEndObject();
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }

        // ---- Writing ---------------------------------------------------------------------------
        private long _commandStart;

        private Utf8JsonWriter BeginCommand()
        {
            _command.Reset(_commandBytes);
            _commandStart = _commandBytes.Length;
            _command.WriteStartObject();
            return _command;
        }

        private void EndCommand(double t, KindOrder order, Section section)
        {
            _command.WriteEndObject();
            _command.Flush();
            _commands.Add(new CommandEntry(t, order, section, _sequence++, _commandStart, (int)(_commandBytes.Length - _commandStart)));
        }

        private static void WriteUnit(Utf8JsonWriter w, ReadOnlySpan<byte> name, int? id, int instance)
        {
            w.WriteStartObject(name);
            if (id is { } i) w.WriteNumber("id"u8, i);
            else w.WriteNull("id"u8);
            w.WriteNumber("instance"u8, instance);
            w.WriteEndObject();
        }

        private void WriteOrZero(Utf8JsonWriter w, ReadOnlySpan<byte> name, RawSlice value)
        {
            w.WritePropertyName(name);
            if (value.IsNone) w.WriteNumberValue(0);
            else value.WriteTo(w, Raw);
        }

        // The script's values before its commands: the fight, the player, the characters and the enemies.
        private ScriptBuffer Head(JsonObject combatant, ScriptBuffer enemies)
        {
            var fight = _head["fight"];
            _actors.TryGetValue(_playerId, out var player);
            string? playerName = player["name"].IsNull ? null : Str(player["name"]);
            string? hero = player["subType"].IsNull ? null : Str(player["subType"]);

            var head = new ScriptBuffer();
            using var w = new Utf8JsonWriter(head);
            w.WriteStartObject();
            w.WriteString("schema"u8, Schema);
            WriteNode(w, "report_code"u8, Copy(_head["report_code"]));
            WriteNode(w, "fight_id"u8, Copy(fight["id"]));
            WriteNode(w, "fight_name"u8, Copy(fight["name"]));
            if (FightDate() is { } date) w.WriteString("fight_date"u8, date);
            else w.WriteNull("fight_date"u8);
            w.WriteNumber("duration_s"u8, Rel(_fightEnd));
            w.WritePropertyName("dungeon_modifiers"u8);
            if (_affixes.IsNone)
            {
                w.WriteStartArray();
                w.WriteEndArray();
            }
            else _affixes.WriteTo(w, Raw);
            w.WriteStartObject("player"u8);
            w.WriteNumber("id"u8, _playerId);
            w.WriteString("name"u8, playerName);
            w.WriteString("hero"u8, hero);
            w.WriteEndObject();
            w.WritePropertyName("combatant_info"u8);
            combatant.WriteTo(w);
            w.WritePropertyName("players"u8);
            PlayersNode().WriteTo(w);
            // The character as it was in this fight (its combatantinfo event).
            w.WritePropertyName("character"u8);
            JsonSerializer.Serialize(w, CharacterReader.FromCombatantInfo(combatant, playerName ?? "player", hero ?? ""));
            w.WritePropertyName("enemies"u8);
            w.Flush();
            var enemyBytes = new byte[enemies.Length];
            enemies.CopyTo(enemyBytes);
            w.WriteRawValue(enemyBytes, skipInputValidation: true);
            w.Flush();
            return head;
        }

        private static void WriteNode(Utf8JsonWriter w, ReadOnlySpan<byte> name, JsonNode? value)
        {
            w.WritePropertyName(name);
            if (value is null) w.WriteNullValue();
            else value.WriteTo(w);
        }

        // Every player's augmentation ranks (buffs other players give scale with the giver's rank).
        private JsonObject PlayersNode()
        {
            var players = new JsonObject();
            foreach (var info in _combatantInfos)
            {
                var e = new RawFightValue(info.RootElement);
                var gearTraits = new Dictionary<string, int>();
                foreach (var item in e["gear"].Items)
                {
                    foreach (var tr in item["traits"].Items)
                    {
                        string name = Str(tr["name"]);
                        gearTraits[name] = gearTraits.GetValueOrDefault(name) + (Int(tr["rank"]) ?? 1);
                    }
                }
                var weaponTraits = new JsonObject();
                foreach (var w in e["weaponTraits"].Items) weaponTraits[(Int(w["id"]) ?? 0).ToString()] = Int(w["points"]) ?? 0;
                int sid = Int(e["sourceID"]) ?? 0;
                players[sid.ToString()] = new JsonObject
                {
                    ["name"] = _actors.TryGetValue(sid, out var who) ? Copy(who["name"]) : null,
                    ["gear_traits"] = new JsonObject(gearTraits.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                    // weapon-tree augmentation IDs are 3,000,000 + datamine augmentation ID
                    ["weapon_traits"] = weaponTraits,
                };
            }
            return players;
        }

        /// <summary>When the fight was logged (UTC, ISO 8601), or null if the raw fight has no report start time.</summary>
        private string? FightDate()
        {
            if (_head["report_start_time"] is not { IsNull: false } start) return null;
            double ms = Num(start) + Num(_head["fight"]["startTime"]);
            return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        // The whole script: its head, its commands in time order, its ignored casts and its observations.
        private byte[] Script(ScriptBuffer head)
        {
            _commands.Sort(static (a, b) =>
            {
                int c = a.T.CompareTo(b.T);
                if (c == 0) c = a.Order.CompareTo(b.Order);
                if (c == 0) c = a.Section.CompareTo(b.Section);
                return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
            });
            ReadOnlySpan<byte> commandsName = ",\"commands\":["u8, ignoredName = "],\"ignored_casts\":"u8,
                damageName = ",\"observations\":{\"damage\":"u8, aurasName = ",\"auras\":"u8, resourcesName = ",\"resources\":"u8,
                channelsName = ",\"channels\":"u8, end = "}}"u8;
            long length = head.Length + commandsName.Length + _commandBytes.Length + Math.Max(0, _commands.Count - 1)
                + ignoredName.Length + _ignoredBytes.Length + damageName.Length + _damageBytes.Length + aurasName.Length
                + _auraBytes.Length + resourcesName.Length + _resourceBytes.Length + channelsName.Length + _channelBytes.Length
                + end.Length;
            var script = new byte[length];
            var rest = script.AsSpan();
            void Put(ReadOnlySpan<byte> bytes, ref Span<byte> rest)
            {
                bytes.CopyTo(rest);
                rest = rest[bytes.Length..];
            }
            void PutBuffer(ScriptBuffer buffer, ref Span<byte> rest)
            {
                buffer.CopyTo(rest);
                rest = rest[(int)buffer.Length..];
            }
            PutBuffer(head, ref rest);
            Put(commandsName, ref rest);
            for (int i = 0; i < _commands.Count; i++)
            {
                if (i > 0) Put(","u8, ref rest);
                var c = _commands[i];
                _commandBytes.CopyTo(c.Start, rest[..c.Length]);
                rest = rest[c.Length..];
            }
            Put(ignoredName, ref rest);
            PutBuffer(_ignoredBytes, ref rest);
            Put(damageName, ref rest);
            PutBuffer(_damageBytes, ref rest);
            Put(aurasName, ref rest);
            PutBuffer(_auraBytes, ref rest);
            Put(resourcesName, ref rest);
            PutBuffer(_resourceBytes, ref rest);
            Put(channelsName, ref rest);
            PutBuffer(_channelBytes, ref rest);
            Put(end, ref rest);
            return script;
        }
    }
}
