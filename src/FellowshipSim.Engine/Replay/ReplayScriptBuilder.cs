using System.Text.Json.Nodes;
using System.Text.Json;

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

    private static readonly HashSet<string> AuraTypes =
    [
        "applybuff", "applybuffstack", "removebuff", "removebuffstack", "refreshbuff",
        "applydebuff", "applydebuffstack", "removedebuff", "removedebuffstack", "refreshdebuff"
    ];

    /// <summary>Smallest change in an enemy's health fraction between two health samples.</summary>
    private const double HealthSampleStep = 0.01;

    /// <summary>A cast event this soon before a begincast of the same ability is a press that did not go through (ms).</summary>
    private const double LoneCastBeforeBeginCastMs = 500;

    /// <summary>Longest a begincast can stay open before it is taken as abandoned (ms).</summary>
    private const double MaxCastWindowMs = 6000;

    /// <summary>Order of commands at the same moment.</summary>
    private static readonly Dictionary<string, int> KindOrder = new()
    {
        ["enemy_arise"] = 0, ["enemy_health"] = 1, ["external_aura"] = 1, ["cancel_cast"] = 2, ["stop_channel"] = 3,
        ["begin_cast"] = 4, ["complete_cast"] = 4, ["cast"] = 4, ["player_revive"] = 1, ["player_death"] = 8, ["enemy_demise"] = 9,
    };

    /// <summary>The players of a raw fight (actor ID, name, hero).</summary>
    public static IReadOnlyList<ReportPlayer> Players(JsonObject raw)
    {
        var friendly = raw["fight"]?["friendlyPlayers"]?.AsArray().Select(x => x!.GetValue<int>()).ToHashSet() ?? [];
        return (raw["master_data"]?["actors"]?.AsArray() ?? [])
            .Where(a => a?["type"]?.GetValue<string>() == "Player" && friendly.Contains(a["id"]!.GetValue<int>()))
            .Select(a => new ReportPlayer(a!["id"]!.GetValue<int>(), a["name"]?.GetValue<string>() ?? "?", a["subType"]?.GetValue<string>() ?? ""))
            .ToList();
    }

    public static JsonObject Build(JsonObject raw, int playerId)
    {
        var fight = raw["fight"]!.AsObject();
        double f0 = Num(fight["startTime"]);
        double fightEnd = Num(fight["endTime"]);
        var actors = new Dictionary<int, JsonNode>();
        foreach (var a in raw["master_data"]!["actors"]!.AsArray()) actors[a!["id"]!.GetValue<int>()] = a;
        var abilityNames = new Dictionary<long, string>();
        foreach (var a in raw["master_data"]!["abilities"]!.AsArray()) abilityNames[(long)Num(a!["gameID"])] = a["name"]?.GetValue<string>() ?? "";
        var events = Dedupe(raw["events"]!.AsArray());

        double Rel(double ts) => Math.Round((ts - f0) / 1000.0, 3);
        bool IsEnemy(int? id) => id is { } i && i != -1 && actors.TryGetValue(i, out var a)
            && a["type"]?.GetValue<string>() == "NPC" && a["name"]?.GetValue<string>() != "Environment";
        string AbilityName(long id) => abilityNames.TryGetValue(id, out var n) ? n : $"ability_{id}";
        JsonObject Unit(int? id, int instance) => new() { ["id"] = id, ["instance"] = instance };

        var commands = new List<JsonObject>();
        var obsDamage = new JsonArray();
        var obsAuras = new JsonArray();
        var obsResources = new JsonArray();
        var obsChannels = new JsonArray();

        // ---- Enemy lifetimes ---------------------------------------------------------------
        var order = new List<(int Id, int Instance)>();
        var firstSeen = new Dictionary<(int, int), double>();
        var lastSeen = new Dictionary<(int, int), double>();
        var deathAt = new Dictionary<(int, int), double>();
        var maxHp = new Dictionary<(int, int), double>();
        var nameOf = new Dictionary<(int, int), string>();
        foreach (var e in events)
        {
            foreach (var prefix in new[] { "source", "target" })
            {
                int? id = Int(e[$"{prefix}ID"]);
                if (!IsEnemy(id)) continue;
                var key = (id!.Value, Instance(e, prefix));
                double ts = Num(e["timestamp"]);
                if (!firstSeen.ContainsKey(key)) { firstSeen[key] = ts; order.Add(key); }
                lastSeen[key] = ts;
                nameOf[key] = actors[key.Item1]["name"]?.GetValue<string>() ?? "";
                if (e[$"{prefix}Resources"]?["maxHitPoints"] is { } mhp && Num(mhp) > 0)
                {
                    maxHp[key] = Math.Max(maxHp.GetValueOrDefault(key), Num(mhp));
                }
            }
            if (Str(e["type"]) == "death" && IsEnemy(Int(e["targetID"])))
            {
                deathAt.TryAdd((Int(e["targetID"])!.Value, Instance(e, "target")), Num(e["timestamp"]));
            }
        }

        var enemies = new JsonArray();
        foreach (var key in order)
        {
            bool died = deathAt.ContainsKey(key);
            double endT = died ? deathAt[key] : lastSeen[key];
            enemies.Add(new JsonObject
            {
                ["id"] = key.Item1, ["instance"] = key.Item2, ["name"] = nameOf[key], ["max_hp"] = maxHp.GetValueOrDefault(key),
                ["arise_t"] = Rel(firstSeen[key]), ["demise_t"] = Rel(endT), ["demise_reason"] = died ? "death" : "last_seen",
            });
            commands.Add(new JsonObject
            {
                ["t"] = Rel(firstSeen[key]), ["kind"] = "enemy_arise", ["enemy"] = Unit(key.Item1, key.Item2),
                ["name"] = nameOf[key], ["max_hp"] = maxHp.GetValueOrDefault(key),
            });
            // An enemy still alive at the end of the fight is not demised.
            if (died || endT < fightEnd - 2000)
            {
                commands.Add(new JsonObject
                {
                    ["t"] = Rel(endT), ["kind"] = "enemy_demise", ["enemy"] = Unit(key.Item1, key.Item2),
                    ["reason"] = died ? "death" : "last_seen",
                });
            }
        }

        // ---- Enemy health (from everyone's damage; health-dependent effects need it) ---------
        var lastFraction = new Dictionary<(int, int), double>();
        foreach (var e in events)
        {
            if (Str(e["type"]) != "damage" || !IsEnemy(Int(e["targetID"]))) continue;
            var res = e["targetResources"];
            if (res?["hitPoints"] is not { } hpNode || res["maxHitPoints"] is not { } maxNode || Num(maxNode) == 0) continue;
            double hp = Num(hpNode), maxNow = Num(maxNode);
            // A killing blow logs the health below 0 as an unsigned 32-bit number (1p7AXmHfwV9CYMxR fight 30 @ 157.097:
            // 4294950962 of 726971).
            if (hp > maxNow) hp = 0;
            var key = (Int(e["targetID"])!.Value, Instance(e, "target"));
            double fraction = hp / maxNow;
            if (lastFraction.TryGetValue(key, out var last) && Math.Abs(fraction - last) < HealthSampleStep) continue;
            lastFraction[key] = fraction;
            commands.Add(new JsonObject
            {
                ["t"] = Rel(Num(e["timestamp"])), ["kind"] = "enemy_health", ["enemy"] = Unit(key.Item1, key.Item2),
                ["hp"] = hp, ["max_hp"] = maxNode.DeepClone(),
            });
        }

        // ---- Player deaths -------------------------------------------------------------------
        // A dead player's events log their health below 0 as an unsigned 32-bit number (above their maximum). Many returns
        // log no resurrect event (a release, or a wipe): the player is back at their first event with health in range.
        bool Alive(JsonNode e, string prefix) => e[$"{prefix}Resources"] is { } r && r["hitPoints"] is { } hpNode
            && r["maxHitPoints"] is { } maxNode && Num(hpNode) > 0 && Num(hpNode) <= Num(maxNode);
        bool dead = false;
        foreach (var e in events)
        {
            string type = Str(e["type"]);
            bool onPlayer = Int(e["targetID"]) == playerId, byPlayer = Int(e["sourceID"]) == playerId;
            if (!dead)
            {
                if (type != "death" || !onPlayer) continue;
                dead = true;
                commands.Add(new JsonObject { ["t"] = Rel(Num(e["timestamp"])), ["kind"] = "player_death" });
                continue;
            }
            if ((type == "resurrect" && onPlayer) || (byPlayer && Alive(e, "source")) || (onPlayer && Alive(e, "target")))
            {
                dead = false;
                commands.Add(new JsonObject { ["t"] = Rel(Num(e["timestamp"])), ["kind"] = "player_revive" });
            }
        }

        // ---- Player actions ------------------------------------------------------------------
        var playerEvents = events.Where(e => Int(e["sourceID"]) == playerId
            && Str(e["type"]) is "begincast" or "cast" or "beginchannel" or "endchannel").ToList();

        // The log has no cancel event: a begincast that never completes was cancelled. The exact moment is unknown, so it
        // is placed at the player's first action after the cast began (actions used while casting without breaking it,
        // e.g. off-GCD spells, are only known to be such once the cast completes).
        JsonObject? openCast = null;
        double openTs = 0;
        var actionsSinceOpen = new List<double>();
        void CancelOpen(double fallbackT)
        {
            var cancel = (JsonObject)openCast!.DeepClone();
            cancel["t"] = actionsSinceOpen.Count > 0 ? actionsSinceOpen[0] : fallbackT;
            cancel["kind"] = "cancel_cast";
            cancel["inferred_time"] = true;
            commands.Add(cancel);
        }

        var ignoredCasts = new JsonArray();
        for (int i = 0; i < playerEvents.Count; i++)
        {
            var e = playerEvents[i];
            long gid = (long)Num(e["abilityGameID"]);
            JsonObject Base() => new()
            {
                ["t"] = Rel(Num(e["timestamp"])), ["ability_id"] = gid, ["ability_name"] = AbilityName(gid),
                ["target"] = Unit(Int(e["targetID"]) ?? -1, Instance(e, "target")),
            };
            double t = Rel(Num(e["timestamp"]));
            switch (Str(e["type"]))
            {
                case "begincast":
                    if (openCast != null) CancelOpen(t);
                    openCast = Base();
                    openTs = Num(e["timestamp"]);
                    actionsSinceOpen.Clear();
                    commands.Add(With(Base(), "kind", "begin_cast"));
                    break;

                case "cast":
                    if (e["fake"]?.GetValue<bool>() == true) break; // duplicate activation marker emitted alongside begincast
                    if (openCast != null && (long)Num(openCast["ability_id"]) == gid)
                    {
                        commands.Add(With(Base(), "kind", "complete_cast"));
                        openCast = null;
                        break;
                    }
                    if (openCast != null && Num(e["timestamp"]) - openTs > MaxCastWindowMs)
                    {
                        CancelOpen(t);
                        openCast = null;
                    }
                    if (openCast != null) actionsSinceOpen.Add(t);
                    // A lone cast event right before the same ability's begincast is not a real activation (the game logs
                    // it when a press does not go through; e.g. Chronoshift @ 124.882 then begincast @ 125.047).
                    bool lone = playerEvents.Skip(i + 1).Take(5).Any(n => Str(n["type"]) == "begincast"
                        && (long)Num(n["abilityGameID"]) == gid && Num(n["timestamp"]) - Num(e["timestamp"]) <= LoneCastBeforeBeginCastMs);
                    if (lone)
                    {
                        ignoredCasts.Add(With(Base(), "reason", "cast event right before its own begincast"));
                        break;
                    }
                    // Instant cast. If a cast is open, the engine decides whether this ability may be used while casting.
                    commands.Add(With(Base(), "kind", "cast"));
                    break;

                case "beginchannel":
                    obsChannels.Add(With(Base(), "kind", "begin_channel"));
                    break;

                case "endchannel":
                    obsChannels.Add(With(Base(), "kind", "end_channel"));
                    commands.Add(With(Base(), "kind", "stop_channel"));
                    break;
            }
        }
        if (openCast != null) CancelOpen(Rel(fightEnd));

        // ---- Auras, damage, resources --------------------------------------------------------
        foreach (var e in events)
        {
            string type = Str(e["type"]);
            int? src = Int(e["sourceID"]), tgt = Int(e["targetID"]);
            if (AuraTypes.Contains(type))
            {
                long gid = (long)Num(e["abilityGameID"]);
                var rec = new JsonObject
                {
                    ["t"] = Rel(Num(e["timestamp"])), ["type"] = type, ["ability_id"] = gid, ["ability_name"] = AbilityName(gid),
                    ["source"] = Unit(src, Instance(e, "source")), ["target"] = Unit(tgt, Instance(e, "target")),
                    ["stack"] = e["stack"]?.DeepClone(), ["duration_ms"] = e["duration"]?.DeepClone(),
                    ["extra_ability_id"] = e["extraAbilityGameID"]?.DeepClone(),
                };
                if (tgt == playerId && src == playerId) obsAuras.Add(rec);
                // Buffs from others on the player, and debuffs from others on enemies: engine inputs.
                else if (tgt == playerId || (IsEnemy(tgt) && src != playerId)) commands.Add(With(rec, "kind", "external_aura"));
                else if (IsEnemy(tgt) && src == playerId) obsAuras.Add(rec);
            }
            else if (type == "damage" && src == playerId && IsEnemy(tgt))
            {
                // Only damage to enemies: party mechanics logged as the player hurting themselves (Xavian's Stagger) are not ours.
                long gid = (long)Num(e["abilityGameID"]);
                obsDamage.Add(new JsonObject
                {
                    ["t"] = Rel(Num(e["timestamp"])), ["ability_id"] = gid, ["ability_name"] = AbilityName(gid),
                    ["target"] = Unit(tgt, Instance(e, "target")),
                    ["amount"] = e["amount"]?.DeepClone() ?? 0, ["unmitigated"] = e["unmitigatedAmount"]?.DeepClone(),
                    ["absorbed"] = e["absorbed"]?.DeepClone() ?? 0, ["blocked"] = e["blocked"]?.DeepClone() ?? 0, ["overkill"] = e["overkill"]?.DeepClone() ?? 0,
                    ["crit"] = Int(e["hitType"]) is HitTypeCrit or HitTypeGrievousCrit, ["hit_type"] = e["hitType"]?.DeepClone(), ["tick"] = e["tick"]?.GetValue<bool>() ?? false,
                });
            }

            if (src == playerId && e["sourceResources"]?["resources"] is JsonArray { Count: > 0 } resources)
            {
                var byType = new JsonObject();
                foreach (var r in resources)
                {
                    byType[Int(r!["type"])!.Value.ToString()] = new JsonObject { ["amount"] = r["amount"]?.DeepClone(), ["max"] = r["max"]?.DeepClone() };
                }
                obsResources.Add(new JsonObject
                {
                    ["t"] = Rel(Num(e["timestamp"])), ["event"] = type, ["ability_id"] = e["abilityGameID"]?.DeepClone(), ["resources"] = byType,
                });
            }
        }

        var sortedCommands = commands
            .OrderBy(c => Num(c["t"]))
            .ThenBy(c => KindOrder.GetValueOrDefault(Str(c["kind"]), 5))
            .ToList();

        // Every player's augmentation ranks (buffs other players give scale with the giver's rank).
        var players = new JsonObject();
        foreach (var e in events.Where(e => Str(e["type"]) == "combatantinfo"))
        {
            var gearTraits = new Dictionary<string, int>();
            foreach (var item in e["gear"]?.AsArray() ?? [])
            {
                foreach (var tr in item?["traits"]?.AsArray() ?? [])
                {
                    string name = Str(tr!["name"]);
                    gearTraits[name] = gearTraits.GetValueOrDefault(name) + (Int(tr["rank"]) ?? 1);
                }
            }
            var weaponTraits = new JsonObject();
            foreach (var w in e["weaponTraits"]?.AsArray() ?? []) weaponTraits[Int(w!["id"])!.Value.ToString()] = Int(w["points"]) ?? 0;
            int sid = Int(e["sourceID"])!.Value;
            players[sid.ToString()] = new JsonObject
            {
                ["name"] = actors.TryGetValue(sid, out var who) ? who["name"]?.DeepClone() : null,
                ["gear_traits"] = new JsonObject(gearTraits.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                // weapon-tree augmentation IDs are 3,000,000 + datamine augmentation ID
                ["weapon_traits"] = weaponTraits,
            };
        }

        actors.TryGetValue(playerId, out var player);
        string? playerName = player?["name"]?.GetValue<string>();
        string? hero = player?["subType"]?.GetValue<string>();
        var combatant = events.FirstOrDefault(e => Str(e["type"]) == "combatantinfo" && Int(e["sourceID"]) == playerId);

        return new JsonObject
        {
            ["schema"] = Schema,
            ["report_code"] = raw["report_code"]?.DeepClone(),
            ["fight_id"] = fight["id"]?.DeepClone(),
            ["fight_name"] = fight["name"]?.DeepClone(),
            ["fight_date"] = FightDate(raw),
            ["duration_s"] = Rel(fightEnd),
            ["player"] = new JsonObject { ["id"] = playerId, ["name"] = playerName, ["hero"] = hero },
            ["combatant_info"] = combatant?.DeepClone(),
            ["players"] = players,
            // The character as it was in this fight (its combatantinfo event).
            ["character"] = combatant == null ? null
                : JsonSerializer.SerializeToNode(CharacterReader.FromCombatantInfo(combatant.AsObject(), playerName ?? "player", hero ?? "")),
            ["enemies"] = enemies,
            ["commands"] = new JsonArray(sortedCommands.Select(c => (JsonNode?)c).ToArray()),
            ["ignored_casts"] = ignoredCasts,
            ["observations"] = new JsonObject
            {
                ["damage"] = obsDamage,
                ["auras"] = obsAuras,
                ["resources"] = obsResources,
                ["channels"] = obsChannels,
            },
        };
    }

    /// <summary>When the fight was logged (UTC, ISO 8601), or null if the raw fight has no report start time.</summary>
    private static string? FightDate(JsonObject raw)
    {
        if (raw["report_start_time"] is not { } start) return null;
        double ms = Num(start) + Num(raw["fight"]?["startTime"]);
        return DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    /// <summary>Page boundaries in the API overlap: drops exact duplicate events, then orders by time.</summary>
    private static List<JsonObject> Dedupe(JsonArray events)
    {
        var seen = new HashSet<string>();
        var list = new List<JsonObject>();
        foreach (var node in events)
        {
            if (node is not JsonObject e) continue;
            if (seen.Add(Canonical(e))) list.Add(e);
        }
        return list.OrderBy(e => Num(e["timestamp"])).ToList();
    }

    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{Canonical(kv.Value)}")) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        null => "null",
        _ => node.ToJsonString(),
    };

    private static JsonObject With(JsonObject o, string key, JsonNode? value)
    {
        o[key] = value;
        return o;
    }

    private static int Instance(JsonNode e, string prefix) => Int(e[$"{prefix}Instance"]) ?? 1;
    private static string Str(JsonNode? n) => n?.GetValue<string>() ?? "";
    private static int? Int(JsonNode? n) => n == null ? null : (int)Num(n);
    private static double Num(JsonNode? n)
    {
        if (n is not JsonValue v) return 0;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<int>(out var i)) return i;
        return 0;
    }
}
