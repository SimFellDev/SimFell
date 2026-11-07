using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.Weapons;

namespace FellowshipSim.Core.Domain;

public abstract class Hero : Actor
{
    protected Hero()
    {
        AddResource(SpiritPoints);
        OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target is { } enemy) EarnSpiritPoints(enemy, enemy.HealthFraction, dmg.SimContext);
        };
        OnEnemyDefeated += (ctx, enemy) => EarnSpiritPoints(enemy, 0.0, ctx);
        OnEnemyDefeated += (ctx, enemy) =>
        {
            if (enemy.Name == BreakerOfChains.CreatureName) Auras.Apply(BreakerOfChains.Buff, ctx.CurrentTime);
        };
        // Spirit of Heroism is applied by every hero's Spirit ability; register it up front so a replay compares it from the start.
        // Cooldown recovery depends on haste and cooldown acceleration: progress every recharge at the old rate when they change.
        Auras.OnGained += (aura, _, now) => AuraChanged(aura, now);
        Auras.OnRemoved += (aura, now) => AuraChanged(aura, now);
    }

    /// <summary>The stats <see cref="CooldownRate"/> reads.</summary>
    private static readonly int CooldownRateStats =
        AuraDef.MaskOf(StatType.CooldownRecovery) | AuraDef.MaskOf(StatType.Haste) | AuraDef.MaskOf(StatType.HasteRating);

    /// <summary>
    /// An aura with cooldown recovery or haste, or one a cooldown acceleration reads, progresses every recharge to now at
    /// the old rate and reads the new one. Speed: any other aura leaves the recharges, whose rate it cannot change.
    /// </summary>
    private void AuraChanged(Aura aura, SimTime now)
    {
        bool acceleration = CooldownAccelerationAuraKeys.Count > 0 && CooldownAccelerationAuraKeys.Contains(aura.Key);
        if (acceleration) _accelerationAuraChanges++;
        if (!acceleration && !aura.Def.Changes(CooldownRateStats)) return;
        foreach (var state in Cooldowns.Values) state.Advance(now);
    }

    // Speed: CooldownRate reads only the auras' cooldown recovery and haste, the auras a cooldown acceleration reads (counted
    // as they change), the accelerations and the stat block. A recharge reads its rate again only after one of them changed;
    // the rate it would read is the rate it has. Each input is its own field and is compared on its own: nearly every read
    // finds them unchanged.
    private int _rateRecovery, _rateHaste, _rateHasteRating, _rateAccelerations, _rateBonuses, _rateCompounds, _rateAccelerationAuras, _rateStatsVersion;
    private StatBlock? _rateStats;
    private long _rateInputsVersion;
    private int _accelerationAuraChanges;

    /// <summary>
    /// Changes whenever something <see cref="CooldownRate"/> reads may have changed. Expired auras are removed first, as the
    /// rate's own read of the auras does.
    /// </summary>
    internal long CooldownRateInputs(SimTime now)
    {
        Auras.CleanExpired(now);
        int recovery = Auras.StatVersion(StatType.CooldownRecovery), haste = Auras.StatVersion(StatType.Haste),
            hasteRating = Auras.StatVersion(StatType.HasteRating);
        var stats = Stats;
        if (recovery != _rateRecovery || haste != _rateHaste || hasteRating != _rateHasteRating
            || CooldownAccelerations.Count != _rateAccelerations || CooldownRecoveryBonuses.Count != _rateBonuses
            || CooldownRecoveryCompounds.Count != _rateCompounds
            || _accelerationAuraChanges != _rateAccelerationAuras
            || !ReferenceEquals(stats, _rateStats) || stats.Version != _rateStatsVersion)
        {
            (_rateRecovery, _rateHaste, _rateHasteRating) = (recovery, haste, hasteRating);
            (_rateAccelerations, _rateBonuses, _rateCompounds, _rateAccelerationAuras, _rateStats, _rateStatsVersion) =
                (CooldownAccelerations.Count, CooldownRecoveryBonuses.Count, CooldownRecoveryCompounds.Count, _accelerationAuraChanges, stats, stats.Version);
            _rateInputsVersion++;
        }
        return _rateInputsVersion;
    }

    /// <summary>The simulation the hero is in (for effects triggered by events that carry no context, e.g. casts).</summary>
    public SimulationContext? SimContext { get; internal set; }

    public EnemyTarget CurrentTarget { get; set; } = null!;
    public Weapon? EquippedWeapon { get; set; }
    public Ability? WeaponAbility { get; set; }
    public List<Ability> Abilities { get; } = new();
    public List<Glyph> Glyphs { get; } = new();
    public List<Augmentation> Augmentations { get; } = new();
    public List<Legendary> Legendaries { get; } = new();
    public List<Gem> Gems { get; } = new();
    public List<Talent> Talents { get; } = new();
    /// <summary>The passive of the relic in slot 1.</summary>
    public RelicPassive? Relic1 { get; set; }
    /// <summary>The passive of the relic in slot 2.</summary>
    public RelicPassive? Relic2 { get; set; }
    /// <summary>The passives of both relic slots.</summary>
    public IEnumerable<RelicPassive> RelicPassives => new[] { Relic1, Relic2 }.OfType<RelicPassive>();

    public Dictionary<string, CooldownState> Cooldowns { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Changes whenever a cooldown state is added to <see cref="Cooldowns"/> or they are cleared.</summary>
    public int CooldownsVersion { get; private set; }
    public SimTime GlobalCooldownEndTime { get; set; } = SimTime.Zero;

    /// <summary>The global cooldown before haste (per the user; hasted).</summary>
    public double GlobalCooldownSeconds { get; set; } = 1.5;

    /// <summary>Haste shortens the global cooldown (Mara's global cooldown is not hasted).</summary>
    public bool GlobalCooldownScalesWithHaste { get; protected set; } = true;

    /// <summary>Parts of the hero's build the engine does not model (filled when the build is imported).</summary>
    public List<string> UnmodeledFeatures { get; } = new();

    /// <summary>Permanent auras (passive talents, gear) applied at the start of every combat.</summary>
    public List<AuraDef> PassiveAuras { get; } = new();

    /// <summary>
    /// Auras that depend on what the engine does not simulate (your own health, standing still, fighting a boss). A sim
    /// keeps them up all fight; a replay applies and removes them when the log does.
    /// </summary>
    public List<AuraDef> SituationalAuras { get; } = new();

    private readonly List<DefensiveProc> _defensiveProcs = new();

    /// <summary>Procs the log proves: they fire in a replay only, at each proof in the log.</summary>
    public IReadOnlyList<DefensiveProc> DefensiveProcs => _defensiveProcs;

    /// <summary>
    /// Declares a proc the log proves. It fires in a replay only: <paramref name="fire"/> runs at each proof of
    /// <paramref name="procKey"/> in the log (its proc evidence entry, of any kind a proc roll reads), with the proof's
    /// enemy, or the current target for a proof without one. <paramref name="fire"/> deals the proc's damage (Xavian's
    /// Solaris, through <see cref="DealDefensiveProcHit"/>) or does what else the proc does (Helena's Second Wind buff).
    /// </summary>
    public void AddDefensiveProc(string procKey, Action<SimulationContext, EnemyTarget?> fire) =>
        _defensiveProcs.Add(new DefensiveProc(procKey, fire));

    /// <summary>
    /// Deals one hit of a defensive proc whose amount the log gives: the log's hit
    /// (<see cref="IOutcomeSource.LoggedHitAmount"/>), in an exact replay and in free dice.
    /// </summary>
    public double DealDefensiveProcHit(SimulationContext ctx, EnemyTarget enemy, int damageLogId, string abilityName) =>
        ctx.ApplyDamage(this, enemy, ctx.Outcomes.LoggedHitAmount(damageLogId, enemy, ctx), damageLogId, abilityName, canCrit: false, 0.0, 1.0);

    /// <summary>
    /// True in a replay (exact or free dice), where every logged pulse (<see cref="AddLoggedPulse"/>) fires at its log hits;
    /// hero code runs the pulse's own periodic chain while it is false, in a sim.
    /// </summary>
    public bool PulsesFollowLog { get; set; }

    /// <summary>
    /// Declares a pulse whose timing the log gives (Xavian's Aura of Solace). A replay calls <paramref name="pulse"/> at
    /// each group of its log hits (the hit evidence of <paramref name="procKey"/>, as for <see cref="AddDefensiveProc"/>),
    /// with <see cref="PulsesFollowLog"/> true. In a sim the hero code's own periodic chain deals the pulses.
    /// </summary>
    public void AddLoggedPulse(string procKey, Action<SimulationContext, EnemyTarget?> pulse) => AddDefensiveProc(procKey, pulse);

    /// <summary>
    /// The <see cref="SituationalAuras"/> up only in combat with a boss (Ruby's Blessing of the Conqueror): a sim without a
    /// boss (<see cref="Engine.FightOptions.Boss"/>) takes them off as each fight starts. A replay follows the log for them.
    /// </summary>
    public List<AuraDef> BossAuras { get; } = new();

    /// <summary>
    /// The class tuning aura every hero has: a damage multiplier the game sets per hero (hero attribute
    /// BalancingDamageAura). 1.0 unless the hero sets its own value in <see cref="Initialize"/>.
    /// </summary>
    public double ClassDamageTuning { get; set; } = 1.0;

    /// <summary>
    /// Talent points every hero has to spend on its tree (in game; combat logs: sww's Rime spends all of them).
    /// </summary>
    public const int TalentPoints = 14;

    /// <summary>The hero's talent tree, as in game.</summary>
    public abstract IReadOnlyList<TalentSlot> TalentTreeSlots { get; }

    /// <summary>The hero's main stat; it names the hero's stat enchants.</summary>
    public abstract MainStat MainStat { get; }

    /// <summary>
    /// Auras built at runtime (from a rank or a gem node) rather than declared as <c>public static readonly AuraDef</c>
    /// constants; see <see cref="Register"/>.
    /// </summary>
    public HashSet<AuraDef> RuntimeAuras { get; } = new();

    /// <summary>
    /// Damage IDs whose hits are the ticks of a damage over time (game effects tagged EffectType.Dot): a tick is no
    /// application of an effect (Kindling rolls on applications).
    /// </summary>
    public HashSet<int> DotDamageIds { get; } = new();

    /// <summary>
    /// The last tick of a damage over time whose execution ended an ability of the hero, with the tick's damage ID. Every
    /// ability end flushes the server's ability queue, so a cast the queue held activates
    /// inside that tick.
    /// </summary>
    public (int DamageLogId, HastedTimer Timer)? QueueFlushingTick { get; set; }

    /// <summary>Extra crit chance against a specific target (e.g. bonuses vs. healthy enemies).</summary>
    public List<Func<EnemyTarget, SimTime, double>> TargetCritChanceBonuses { get; } = new();

    /// <summary>Damage multipliers against a specific target (e.g. bonuses vs. low-health enemies).</summary>
    public List<Func<EnemyTarget, SimTime, double>> TargetDamageMultipliers { get; } = new();

    /// <summary>Damage multipliers for specific abilities that depend on the moment (e.g. scaling with current Spirit).</summary>
    public List<Func<Ability, SimTime, double>> AbilityDamageMultipliers { get; } = new();

    /// <summary>Cooldown acceleration for specific abilities (a fraction added to the recovery rate).</summary>
    public List<Func<Ability, SimTime, double>> CooldownAccelerations { get; } = new();

    /// <summary>Multiplier sources of the Cooldown Recovery attribute, whose bonus part the game calculates (Invictus).</summary>
    public List<Func<SimTime, double>> CooldownRecoveryBonuses { get; } = new();

    /// <summary>MultiplyCompound sources of the Cooldown Recovery attribute, whose value the game calculates (Event Horizon).</summary>
    public List<Func<SimTime, double>> CooldownRecoveryCompounds { get; } = new();

    /// <summary>Keys of the auras a cooldown rate source reads, other than through haste (e.g. Kill Em All).</summary>
    public HashSet<string> CooldownAccelerationAuraKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Seconds added to Spirit of Heroism.</summary>
    public double SpiritOfHeroismDurationBonus { get; set; }

    /// <summary>Auras active exactly while Spirit of Heroism is active.</summary>
    public List<AuraDef> SpiritOfHeroismAuras { get; } = new();

    /// <summary>Spirit of Heroism (effect 2253, granted by every hero's Spirit ability): a Haste bonus.</summary>
    public const int SpiritOfHeroismEffectId = 2253;
    public const double SpiritOfHeroismSeconds = 20.0;
    public const double SpiritOfHeroismHaste = 0.3;

    /// <summary>
    /// Spirit of Heroism: every hero's Spirit ability grants a Haste bonus for a duration. Combat logs (4 deaths): it stays through
    /// the hero's death.
    /// </summary>
    public static readonly AuraDef SpiritOfHeroism = AuraDef.Effect("Spirit of Heroism", SpiritOfHeroismEffectId, SpiritOfHeroismSeconds,
        modifiers: StatModifier.Add(StatType.Haste, SpiritOfHeroismHaste)) with { KeepsOnDeath = true };

    /// <summary>
    /// Spirit of Heroism aura of this hero's Spirit ability: <see cref="SpiritOfHeroism"/>, or the hero's own where its
    /// Spirit ability grants another effect.
    /// </summary>
    public virtual AuraDef SpiritOfHeroismAura => SpiritOfHeroism;

    /// <summary>A hit of the hero landed. Handlers run in the order they subscribed, with those of <see cref="OnDamageDealtBy"/> and <see cref="Ability.Hit"/>.</summary>
    public event DamageDealtHandler? OnDamageDealt
    {
        add { if (value != null) Subscribe(new DamageDealtSubscriber(null, null, value)); }
        remove
        {
            int i = _damageDealt.FindLastIndex(s => s.Ability == null && s.DamageId == null && s.Handler == value);
            if (i < 0) return;
            _damageDealt.RemoveAt(i);
            ForgetDamageDealtTables();
        }
    }

    /// <summary>Subscribes <paramref name="handler"/> to the hits with damage ID <paramref name="damageId"/> alone.</summary>
    public void OnDamageDealtBy(int damageId, DamageDealtHandler handler) =>
        Subscribe(new DamageDealtSubscriber(damageId, null, handler));

    /// <summary>Raises <paramref name="ability"/>'s <see cref="Ability.Hit"/> for the hits with its <see cref="Ability.DamageLogId"/>.</summary>
    internal void SubscribeHits(Ability ability) => Subscribe(new DamageDealtSubscriber(null, ability, null));

    /// <summary>
    /// One subscriber to landed hits: a handler of every hit, a handler of one damage ID, or an ability's
    /// <see cref="Ability.Hit"/>. An ability's damage ID is read when the table of an ID is built, after its constructor.
    /// </summary>
    private readonly record struct DamageDealtSubscriber(int? DamageId, Ability? Ability, DamageDealtHandler? Handler)
    {
        public bool Receives(int damageId) =>
            Ability != null ? Ability.DamageLogId == damageId : DamageId == null || DamageId == damageId;
    }

    private readonly List<DamageDealtSubscriber> _damageDealt = new();
    // Speed: each damage ID has its own subscribers, in subscription order, so a hit reaches only the handlers it concerns.
    private readonly Dictionary<int, DamageDealtSubscriber[]> _damageDealtById = new();
    // Speed: the tables of recent damage IDs, one per low bits of the ID, ahead of the dictionary.
    private const int RecentDamageIds = 64;
    private readonly int[] _recentDamageIds = NoRecentDamageIds();
    private readonly DamageDealtSubscriber[][] _recentSubscribers = new DamageDealtSubscriber[RecentDamageIds][];

    private static int[] NoRecentDamageIds()
    {
        var ids = new int[RecentDamageIds];
        Array.Fill(ids, int.MinValue);
        return ids;
    }

    private void ForgetDamageDealtTables()
    {
        _damageDealtById.Clear();
        Array.Fill(_recentDamageIds, int.MinValue);
        Array.Clear(_recentSubscribers);
    }

    private void Subscribe(DamageDealtSubscriber subscriber)
    {
        _damageDealt.Add(subscriber);
        ForgetDamageDealtTables();
    }
    public event Action<Ability, EnemyTarget?, SimTime>? OnCastStarted;
    public event Action<Ability, EnemyTarget?, SimTime>? OnCastCompleted;
    public event Action<Ability, EnemyTarget?, SimTime>? OnCastCancelled;
    public event Action<Ability, EnemyTarget?, SimTime, bool>? OnChannelEnded;
    /// <summary>A channel ticked (after the ability's own tick effects); the double is the tick's fraction (below 1 for a partial tick).</summary>
    public event Action<Ability, EnemyTarget?, double, SimulationContext>? OnChannelTick;
    /// <summary>Resource was spent (or counts as spent).</summary>
    public event Action<ResourceDef, double, SimTime>? OnResourceSpent;
    /// <summary>Resource was gained past its maximum: the double is the amount that overflowed.</summary>
    public event Action<ResourceDef, double, SimTime>? OnResourceOverflow;
    /// <summary>
    /// Resource was gained: the amount added, and the enemy it came from if any (e.g. Anima from a Frost Bolt's target).
    /// Not raised for Spirit Refunds, which give back what was spent.
    /// </summary>
    public event Action<ResourceDef, double, EnemyTarget?, SimulationContext>? OnResourceGained;
    /// <summary>A Spirit Refund gave back the resource spent (a moment after the spend).</summary>
    public event Action<ResourceDef, double, SimulationContext>? OnResourceRefunded;
    /// <summary>Spirit of Heroism started / ended.</summary>
    public event Action<SimulationContext>? SpiritOfHeroismStarted;
    public event Action<SimulationContext>? SpiritOfHeroismEnded;
    /// <summary>
    /// A Spirit Refund triggered on a spend of the resource (the refund itself arrives a moment later), or with no resource
    /// for a hero whose refund is not a spend.
    /// </summary>
    public event Action<ResourceDef?, double, SimulationContext>? OnSpiritRefund;
    /// <summary>A cast is about to resolve (also free casts from procs): talents add to it here (see <see cref="CastBonus"/>).</summary>
    public event Action<CastBonus>? OnCastResolving;

    /// <summary>An enemy in combat was defeated (not one that left combat alive), as it is removed.</summary>
    public event Action<SimulationContext, EnemyTarget>? OnEnemyDefeated;

    internal void TriggerEnemyDefeated(SimulationContext ctx, EnemyTarget enemy) => OnEnemyDefeated?.Invoke(ctx, enemy);

    /// <summary>Raised as the hero interrupts an enemy's cast, with the ability it was casting.</summary>
    public event Action<SimulationContext, EnemyTarget, string>? OnInterrupt;

    internal void TriggerInterrupt(SimulationContext ctx, EnemyTarget enemy, string ability) => OnInterrupt?.Invoke(ctx, enemy, ability);

    /// <summary>The hero applied a harmful effect that deals no damage of its own to an enemy (a debuff such as Bursting Ice's).</summary>
    public event Action<SimulationContext, EnemyTarget>? OnHarmfulEffectApplied;

    public void TriggerHarmfulEffectApplied(SimulationContext ctx, EnemyTarget enemy) => OnHarmfulEffectApplied?.Invoke(ctx, enemy);

    /// <summary>The hero died.</summary>
    public event Action<SimulationContext>? OnDied;

    public void TriggerDied(SimulationContext ctx) => OnDied?.Invoke(ctx);

    /// <summary>The cast in progress, if any (abilities with a cast time between begin and completion).</summary>
    public ActiveCast? CurrentCast { get; internal set; }

    /// <summary>The channel in progress, if any.</summary>
    public ActiveChannel? CurrentChannel { get; internal set; }

    public void TriggerDamageDealt(in DamageContext hit)
    {
        int id = hit.AbilityId;
        int recent = id & (RecentDamageIds - 1);
        DamageDealtSubscriber[] subscribers;
        if (_recentDamageIds[recent] == id) subscribers = _recentSubscribers[recent];
        else
        {
            if (!_damageDealtById.TryGetValue(id, out subscribers!)) subscribers = DamageDealtSubscribers(id);
            _recentDamageIds[recent] = id;
            _recentSubscribers[recent] = subscribers;
        }
        foreach (var subscriber in subscribers)
        {
            if (subscriber.Ability != null) subscriber.Ability.RaiseHit(in hit);
            else subscriber.Handler!(in hit);
        }
    }
    // A method of its own, so TriggerDamageDealt allocates the closure only for an ability ID new to the hero: a closure's
    // captures are allocated where their scope begins.
    private DamageDealtSubscriber[] DamageDealtSubscribers(int id)
    {
        var subscribers = _damageDealt.Where(s => s.Receives(id)).ToArray();
        _damageDealtById[id] = subscribers;
        return subscribers;
    }

    public void TriggerCastStarted(Ability a, EnemyTarget? t, SimTime now = default) => OnCastStarted?.Invoke(a, t, now);
    public void TriggerCastCompleted(Ability a, EnemyTarget? t, SimTime now = default) => OnCastCompleted?.Invoke(a, t, now);
    public void TriggerCastCancelled(Ability a, EnemyTarget? t, SimTime now = default) => OnCastCancelled?.Invoke(a, t, now);
    public void TriggerChannelEnded(Ability a, EnemyTarget? t, SimTime now, bool clipped) => OnChannelEnded?.Invoke(a, t, now, clipped);
    public void TriggerChannelTick(Ability a, EnemyTarget? t, double fraction, SimulationContext ctx) => OnChannelTick?.Invoke(a, t, fraction, ctx);

    // ------------------------------------------------------------------ resources and Spirit Refund

    /// <summary>Spirit Points toward the Spirit ability (every hero).</summary>
    /// <summary>
    /// Spirit Points: they fill the Spirit ability (game data). Every hero regains them on a fixed period
    /// in combat and earns them as enemies lose health (combat logs report them as resource type 4).
    /// </summary>
    public static readonly ResourceDef SpiritPoints = new("Spirit", Max: 100);
    public const double SpiritPointRegenSeconds = 3.0;
    public const double SpiritPointRegenAmount = 1.0;
    /// <summary>The Spirit ability's cost (game data).</summary>
    public const double SpiritAbilityBaseCost = 100.0;

    /// <summary>
    /// Game data scaler for Spirit Points from damage, with no party size or keystone term.
    /// </summary>
    public const double SpiritFromDamageScaler = 0.25;

    /// <summary>
    /// Spirit Points for the enemy's health lost down to <paramref name="healthFraction"/>. In the game data it is
    /// SpiritPointValue x min(MaxHealth, |TotalHealthChange|) / MaxHealth x <see cref="SpiritFromDamageScaler"/> on each damage
    /// event. A heal runs no gain, and a healed enemy gives Spirit Points again. Each party member's ability hears that
    /// member's outgoing `Event.Damage` alone and grants the gain to
    /// every living party member, the owner included, so health lost
    /// to any member counts. `TotalHealthChange` includes overkill, and this method reads health, which has none: it gains
    /// fewer Spirit Points on killing blows. Heroes/Mara/NOTES.md, section Resources, has the fights.
    /// </summary>
    public void EarnSpiritPoints(EnemyTarget enemy, double healthFraction, SimulationContext ctx)
    {
        if (enemy.SpiritPointValue <= 0 || healthFraction >= enemy.SpiritHealthFraction) return;
        double lost = enemy.SpiritHealthFraction - healthFraction;
        enemy.SpiritHealthFraction = healthFraction;
        GainResource(SpiritPoints, lost * enemy.SpiritPointValue * SpiritFromDamageScaler, ctx);
    }

    /// <summary>Spirit Points a Spirit Refund grants (game data; Rime sets its own).</summary>
    public double SpiritPointsPerSpiritRefund { get; protected set; } = 1.0;

    /// <summary>Share taken off the Spirit ability's cost (Blessing of the Prophet).</summary>
    public double SpiritAbilityCostReduction { get; set; }

    public double SpiritAbilityCost => SpiritAbilityBaseCost * (1.0 - SpiritAbilityCostReduction);

    /// <summary>Raises the maximum Spirit Points (Ancestral Surge).</summary>
    public void AddMaxSpiritPoints(double amount) => Resources.Get(SpiritPoints).Max += amount;

    /// <summary>Starts what runs for the whole fight on its own: the Spirit Point regeneration.</summary>
    public void BeginFight(SimulationContext ctx)
    {
        Action<SimulationContext>? regen = null;
        void Regen(SimulationContext c)
        {
            GainResource(SpiritPoints, SpiritPointRegenAmount, c);
            c.Schedule(SimTime.FromSeconds(SpiritPointRegenSeconds), regen, "Spirit Point regeneration");
        }
        regen = Regen;
        ctx.Schedule(SimTime.FromSeconds(SpiritPointRegenSeconds), regen, "Spirit Point regeneration");
        OnFightBegins(ctx);
    }

    /// <summary>Starts what runs for the whole fight on its own for one hero (Mara: Energy regeneration).</summary>
    protected virtual void OnFightBegins(SimulationContext ctx) { }

    /// <summary>Adds what a resource regenerated since its last event, up to <paramref name="now"/> (Mara: Energy).</summary>
    public virtual void RegenerateResources(SimTime now) { }

    /// <summary>The resources this hero has (declared with <see cref="AddResource"/> in <see cref="Initialize"/>).</summary>
    public List<ResourceDef> ResourceDefs { get; } = new();

    protected void AddResource(ResourceDef resource)
    {
        ResourceDefs.Add(resource);
        Resources.Register(resource, resource.Max, resource.Initial);
    }

    /// <summary>The resource named <paramref name="aplName"/> in action lists (its <see cref="ResourceDef.AplName"/> or an alias).</summary>
    public ResourceDef? FindResource(string aplName) =>
        aplName == SpiritPoints.AplName ? SpiritPoints
            : ResourceDefs.FirstOrDefault(r => r.AplName == aplName || r.Aliases.Contains(aplName));

    public double ResourceAmount(ResourceDef resource) => Resources.Get(resource).Current;

    /// <summary>
    /// Adds <paramref name="amount"/> up to the maximum (<see cref="OnResourceGained"/>); the part past the maximum
    /// overflows (<see cref="OnResourceOverflow"/>). <paramref name="from"/> is the enemy it came from, if any. Returns the
    /// amount added.
    /// </summary>
    public double GainResource(ResourceDef resource, double amount, SimulationContext? ctx, EnemyTarget? from = null) =>
        AddToResource(resource, amount, ctx, from, isGain: true);

    private double AddToResource(ResourceDef resource, double amount, SimulationContext? ctx, EnemyTarget? from, bool isGain)
    {
        var state = Resources.Get(resource);
        double added = Math.Min(amount, Math.Max(0.0, state.Max - state.Current));
        state.Current += added;
        if (amount - added > 0) OnResourceOverflow?.Invoke(resource, amount - added, ctx?.CurrentTime ?? default);
        if (isGain && added > 0 && ctx != null) OnResourceGained?.Invoke(resource, added, from, ctx);
        return added;
    }

    /// <summary>Lets talents add to a cast about to resolve (<see cref="OnCastResolving"/>); see <see cref="Ability.ResolveCast(SimulationContext, EnemyTarget, object?)"/>.</summary>
    public CastBonus PrepareCast(Ability ability, EnemyTarget? target, SimTime now, object? source = null, SimulationContext? ctx = null)
    {
        var cast = new CastBonus(ability, target, now, source, ctx);
        OnCastResolving?.Invoke(cast);
        return cast;
    }

    /// <summary>
    /// Spends <paramref name="amount"/>. When it is the hero's <see cref="SpiritRefundResource"/>, a Spirit Refund may give
    /// it back.
    /// </summary>
    public void SpendResource(ResourceDef resource, double amount, SimulationContext? ctx)
    {
        var state = Resources.Get(resource);
        state.Current = Math.Max(0.0, state.Current - amount);
        NotifyResourceSpent(resource, amount, ctx?.CurrentTime ?? default);
        if (amount <= 0 || ctx == null || resource != SpiritRefundResource) return;
        if (!TriggerSpiritRefund(resource, amount, ctx)) return;
        // The resource comes back a moment after the spend.
        ctx.Schedule(SimTime.FromSeconds(SpiritRefundDelaySeconds), c =>
        {
            AddToResource(resource, amount, c, null, isGain: false);
            OnResourceRefunded?.Invoke(resource, amount, c);
        }, "Spirit Refund");
    }

    /// <summary>
    /// Rolls a Spirit Refund. When it triggers, Rising Spirit and the Spirit Points come at once and
    /// <see cref="OnSpiritRefund"/> is raised. A hero whose Spirit Refund comes from using an ability rather than spending
    /// a resource (Gunde: abilities that apply Rend) calls this with no <paramref name="resource"/> and gives what its
    /// refund gives itself.
    /// </summary>
    protected bool TriggerSpiritRefund(ResourceDef? resource, double amount, SimulationContext ctx)
    {
        if (!RollSpiritRefund(ctx)) return false;
        Auras.Apply(RisingSpirit, ctx.CurrentTime);
        OnSpiritRefund?.Invoke(resource, amount, ctx);
        GainResource(SpiritPoints, SpiritPointsPerSpiritRefund, ctx);
        return true;
    }

    /// <summary>Tells effects that count spent resource (e.g. cooldown reductions per point spent) that it was spent.</summary>
    public void NotifyResourceSpent(ResourceDef resource, double amount, SimTime now) => OnResourceSpent?.Invoke(resource, amount, now);

    /// <summary>The resource a Spirit Refund gives back: the hero's spender resource (e.g. Rime's Winter Orbs).</summary>
    public ResourceDef? SpiritRefundResource { get; protected set; }

    public const string SpiritRefundProc = "spirit_refund";
    /// <summary>Combat logs (10 Rime logs): refunded resource shows up again shortly after the spend.</summary>
    public const double SpiritRefundDelaySeconds = 0.2;

    /// <summary>Rising Spirit (effect 3115): a primary stat bonus per stack, from Spirit Refunds.</summary>
    public const int RisingSpiritEffectId = 3115;
    public const double RisingSpiritSeconds = 10.0;
    public const int RisingSpiritMaxStacks = 10;
    public const double RisingSpiritPrimaryStatPerStack = 0.05;
    /// <summary>Combat logs (10 Rime logs): each further Spirit Refund adds time to the time left instead of restarting it.</summary>
    public const double RisingSpiritReapplySeconds = 2.0;
    /// <summary>
    /// Game data sets the limit. The game adds time per stack to the effect's total and
    /// stops at the limit, so a stack past that leaves the expiry. Combat logs (23 Mara fights of data/fights): no buff runs
    /// over the limit, and the buffs at the highest stack counts end at it. T2qLPpmMkrAtJ9B1 fight 18: applied 95.261,
    /// removed 115.267. The removal of 191 of Rime's 193 chains of 2 or more stacks and 939 of Helena's 940 fits it.
    /// </summary>
    public const double RisingSpiritDurationLimitSeconds = 20.0;

    public static readonly AuraDef RisingSpirit = AuraDef.Effect("Rising Spirit", RisingSpiritEffectId, RisingSpiritSeconds,
        RisingSpiritMaxStacks, StatModifier.Add(StatType.PrimaryAttributeMultiplier, RisingSpiritPrimaryStatPerStack))
        with { ReapplyExtendsSeconds = RisingSpiritReapplySeconds, ExtendedDurationLimitSeconds = RisingSpiritDurationLimitSeconds };

    /// <summary>Effects that make a Spirit Refund certain (e.g. Undulating Spirit): each returns true if it guaranteed this one.</summary>
    public List<Func<SimTime, bool>> GuaranteedSpiritRefunds { get; } = new();

    private bool RollSpiritRefund(SimulationContext ctx)
    {
        foreach (var guaranteed in GuaranteedSpiritRefunds)
        {
            if (guaranteed(ctx.CurrentTime)) return true;
        }
        // Spirit Refund chance is a diminishing-returns function of Spirit (per the user).
        double spirit = SpiritPercent(ctx.CurrentTime);
        return ctx.Outcomes.RollProc(SpiritRefundProc, spirit / (1.0 + spirit), ctx);
    }

    // ------------------------------------------------------------------ auras

    /// <summary>
    /// Lists an aura built at runtime in <see cref="RuntimeAuras"/>, so tools that look at every aura the hero can have
    /// (the replay) see it. Declared AuraDef constants need no call.
    /// </summary>
    public AuraDef Register(AuraDef def)
    {
        RuntimeAuras.Add(def);
        return def;
    }

    /// <summary>Activates Spirit of Heroism and the auras that last while it does.</summary>
    public void ActivateSpiritOfHeroism(SimulationContext ctx)
    {
        var heroism = SpiritOfHeroismAura.WithDuration(SpiritOfHeroismAura.Duration.TotalSeconds + SpiritOfHeroismDurationBonus);
        var applied = Auras.Apply(heroism, ctx.CurrentTime);
        foreach (var aura in SpiritOfHeroismAuras) Auras.Apply(aura, ctx.CurrentTime);
        SpiritOfHeroismStarted?.Invoke(ctx);
        // The companions end with Spirit of Heroism itself (on its actual expiry, which includes the server-frame lag).
        ctx.ScheduleAt(applied.ExpiresAt, c =>
        {
            if (Auras.HasAura(heroism, c.CurrentTime)) return;
            foreach (var aura in SpiritOfHeroismAuras) Auras.Remove(aura, c.CurrentTime);
            SpiritOfHeroismEnded?.Invoke(c);
        }, "Spirit of Heroism ends");
    }

    public double AbilityDamageMultiplier(Ability ability, SimTime now)
    {
        double mult = 1.0;
        foreach (var m in AbilityDamageMultipliers) mult *= m(ability, now);
        return mult;
    }

    /// <summary>
    /// Cooldown seconds passing in one real second on <paramref name="ability"/>'s cooldown: the Cooldown Recovery
    /// attribute, or 1 for a cooldown outside it, plus Haste on a charge effect with Haste, plus the time rates on this
    /// cooldown. No source multiplies these terms.
    /// </summary>
    public double CooldownRate(Ability ability, SimTime now)
    {
        double rate = (ability.IgnoresCooldownRecovery ? 1.0 : CooldownRecovery(now)) + CooldownAcceleration(ability, now);
        return ability.ChargesRechargeWithHaste ? rate + GetHaste(now) : rate;
    }

    /// <summary>
    /// The Cooldown Recovery attribute: multiplier sources each add their bonus part (Chronoshift, Strand of
    /// Eternity, Invictus), and each compound source multiplies the sum (Event Horizon).
    /// </summary>
    public double CooldownRecovery(SimTime now)
    {
        double recovery = 1.0 + Auras.SumBonuses(StatType.CooldownRecovery, now);
        foreach (var bonus in CooldownRecoveryBonuses) recovery += bonus(now);
        foreach (var compound in CooldownRecoveryCompounds) recovery *= compound(now);
        return recovery;
    }

    /// <summary>Time rates on one ability's cooldown (e.g. The Monarch for Major abilities), added outside the attribute.</summary>
    public double CooldownAcceleration(Ability ability, SimTime now)
    {
        double accel = 0.0;
        foreach (var a in CooldownAccelerations) accel += a(ability, now);
        return accel;
    }

    // ------------------------------------------------------------------ stats

    /// <summary>Primary attribute after all multipliers.</summary>
    /// <summary>
    /// Main Stat, in the game's layers: the pool (base, gear, flat gem bonuses) with pool multipliers (Torment of
    /// Bael'Aurum, Drakheim's Absolution), then x (1 + summed adds: Mote of Creation, Rising Spirit) x (1 + summed
    /// "increased" bonuses: Martial Initiative, Willful Momentum, Vengeful Soul, Hidden Power) x each true multiplier
    /// (Might of the Minotaur, Ancestral Surge, Ancient's Wisdom, Draconic Might). Source: community breakdown from game
    /// files and in-game tests.
    /// </summary>
    public double PrimaryAttribute(SimTime now)
    {
        if (IsCurrent(ref _primaryAttribute, StatType.PrimaryAttribute, StatType.PrimaryAttributeMultiplier, now)) return _primaryAttribute.Value;
        double value = Auras.Fold(StatType.PrimaryAttribute, Stats.PrimaryStat, now);
        return _primaryAttribute.Value = value * Auras.Fold(StatType.PrimaryAttributeMultiplier, 1.0, now);
    }

    public double CritChance(SimTime now) =>
        IsCurrent(ref _critChance, StatType.CritRating, StatType.CritChance, now)
            ? _critChance.Value
            : _critChance.Value = StatBlock.WithExtraRating(Stats.CritPercent, Stats.CritRating, Auras.Fold(StatType.CritRating, 0.0, now))
                                  + Auras.Fold(StatType.CritChance, 0.0, now);

    public double CritChanceAgainst(EnemyTarget? target, SimTime now)
    {
        double chance = CritChance(now);
        if (target != null)
        {
            foreach (var bonus in TargetCritChanceBonuses) chance += bonus(target, now);
        }
        return chance;
    }

    /// <summary>Damage multiplier of a critical hit.</summary>
    public double CritMultiplier(SimTime now) => Auras.Fold(StatType.CritMultiplier, StatBlock.BaseCritMultiplier, now);

    /// <summary>
    /// Total haste at <paramref name="now"/>: rating haste (with rating buffs) plus haste from auras. The game's Haste
    /// attribute is 1 + haste (hero baseAttributes Haste 1), so a multiplying haste modifier scales 1 + haste. Combat logs
    /// (4Ln6PQZCRzc7Wpta fight 77, Shadow's Defeat "Haste Multiplicitive"): Freezing Torrent ticks under it come every
    /// base period over the multiplier x (1 + haste) (log times 726.190, 709.624 and 379.487, the last with Spirit of
    /// Heroism).
    /// </summary>
    public double GetHaste(SimTime now) =>
        IsCurrent(ref _haste, StatType.HasteRating, StatType.Haste, now)
            ? _haste.Value
            : _haste.Value = Math.Max(0.0,
                Auras.Fold(StatType.Haste,
                    1.0 + StatBlock.WithExtraRating(Stats.HastePercent, Stats.HasteRating, Auras.Fold(StatType.HasteRating, 0.0, now)), now) - 1.0);

    /// <summary>Maximum health at <paramref name="now"/>: the stat block's, with the auras' MaxHealth modifiers on the whole value.</summary>
    public double MaxHealth(SimTime now) => Auras.Fold(StatType.MaxHealth, Stats.MaxHealth, now);

    public double Expertise(SimTime now) =>
        IsCurrent(ref _expertise, StatType.ExpertiseRating, StatType.Expertise, now)
            ? _expertise.Value
            : _expertise.Value = StatBlock.WithExtraRating(Stats.ExpertisePercent, Stats.ExpertiseRating, Auras.Fold(StatType.ExpertiseRating, 0.0, now))
                                 + Auras.Fold(StatType.Expertise, 0.0, now);

    /// <summary>Spirit as a fraction (a fraction, not a percentage).</summary>
    public double SpiritPercent(SimTime now) =>
        IsCurrent(ref _spirit, StatType.SpiritRating, StatType.Spirit, now)
            ? _spirit.Value
            : _spirit.Value = StatBlock.WithExtraRating(Stats.SpiritPercent, Stats.SpiritRating, Auras.Fold(StatType.SpiritRating, 0.0, now))
                              + Auras.Fold(StatType.Spirit, 0.0, now);

    // Speed: each of these stats is kept until an aura with a modifier of one of its two stats changes, or the stat block
    // changes. A hit then reads Main Stat, crit chance, haste, Expertise and Spirit without folding auras or applying
    // diminishing returns again. The kept value is the one a new computation would give.
    private DerivedStat _primaryAttribute, _critChance, _haste, _expertise, _spirit;

    /// <summary>
    /// True when <paramref name="stat"/> was computed from the current auras of <paramref name="first"/> and
    /// <paramref name="second"/> and the current stat block; otherwise it takes their versions for the value about to be
    /// computed. Expired auras are removed first, as every read of the auras does.
    /// </summary>
    private bool IsCurrent(ref DerivedStat stat, StatType first, StatType second, SimTime now)
    {
        Auras.CleanExpired(now);
        return stat.IsCurrent(Auras.StatVersion(first), Auras.StatVersion(second), Stats);
    }

    /// <summary>A stat computed from two aura stats and the stat block, with the versions it was computed at.</summary>
    private struct DerivedStat
    {
        public double Value;
        private int _first, _second, _blockVersion;
        private StatBlock? _block;

        public bool IsCurrent(int first, int second, StatBlock block)
        {
            if (ReferenceEquals(_block, block) && _first == first && _second == second && _blockVersion == block.Version) return true;
            (_first, _second, _block, _blockVersion) = (first, second, block, block.Version);
            return false;
        }
    }

    /// <summary>Outgoing damage multiplier from auras ("DamageScale").</summary>
    public double DamageScale(SimTime now) => Auras.Fold(StatType.DamageScale, 1.0, now);

    public double DamageMultiplierAgainst(EnemyTarget? target, SimTime now)
    {
        double mult = 1.0;
        if (target != null)
        {
            foreach (var m in TargetDamageMultipliers) mult *= m(target, now);
        }
        return mult;
    }

    // ------------------------------------------------------------------ cooldowns and charges

    public CooldownState CooldownOf(Ability ability)
    {
        // Speed: an ability of this hero keeps its state until the hero's states change.
        if (ability.KnownCooldownVersion == CooldownsVersion && ReferenceEquals(ability.Owner, this) && ability.KnownCooldown is { } known) return known;
        if (!Cooldowns.TryGetValue(ability.Name, out var state))
        {
            state = NewCooldownState(ability);
            Cooldowns[ability.Name] = state;
            CooldownsVersion++;
        }
        (ability.KnownCooldown, ability.KnownCooldownVersion) = (state, CooldownsVersion);
        return state;
    }

    private CooldownState NewCooldownState(Ability ability) => new(ability.MaxCharges, this, ability);

    public bool IsCooldownReady(string name, SimTime now) =>
        !Cooldowns.TryGetValue(name, out var state) || state.ChargesAt(now) > 0;

    public double GetCooldownRemains(string name, SimTime now) =>
        Cooldowns.TryGetValue(name, out var state) && state.ChargesAt(now) == 0
            ? Math.Max(0.0, (state.NextChargeAt - now).TotalSeconds)
            : 0.0;

    public void ReduceCooldown(string name, SimTime amount, SimTime now = default)
    {
        if (Cooldowns.TryGetValue(name, out var state)) state.Reduce(amount, now);
    }

    public void ReduceCooldown<T>(SimTime amount, SimTime now = default) where T : Ability
    {
        var ability = GetAbility<T>();
        if (ability != null) ReduceCooldown(ability.Name, amount, now);
    }

    public bool IsCooldownReady<T>(SimTime now) where T : Ability
    {
        var ability = GetAbility<T>();
        return ability == null || IsCooldownReady(ability.Name, now);
    }

    public double GetCooldownRemains<T>(SimTime now) where T : Ability
    {
        var ability = GetAbility<T>();
        return ability != null ? GetCooldownRemains(ability.Name, now) : 0.0;
    }

    public abstract void Initialize();

    /// <summary>
    /// The last step of building the hero (<see cref="Import.HeroImporter"/>), once its talents, gear and legendaries are bound: what
    /// depends on the whole build is set here (Ardeos: the global cooldown a legendary item shortens).
    /// </summary>
    public virtual void FinalizeBuild() { }
    public abstract void ResetForIteration();

    public Ability? GetAbility(string name) =>
        Abilities.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The ability of exactly type <typeparamref name="T"/>, else the first built on it (GetAbility&lt;GlacialBlast&gt; is
    /// Glacial Blast itself, not Talon Strike).
    /// </summary>
    public T? GetAbility<T>() where T : Ability =>
        Abilities.OfType<T>().FirstOrDefault(a => a.GetType() == typeof(T)) ?? Abilities.OfType<T>().FirstOrDefault();

    public bool HasTalent<T>() where T : Talent => Talents.OfType<T>().Any();

    public T? GetTalent<T>() where T : Talent =>
        Talents.OfType<T>().FirstOrDefault();


    /// <summary>The rolls since each bad luck protected proc last procced (<see cref="RollBadLuckProc"/>), by its key.</summary>
    private readonly Dictionary<string, int> _badLuckMisses = new(StringComparer.Ordinal);

    /// <summary>
    /// Rolls the proc <paramref name="procKey"/> with bad luck protection (<see cref="BadLuckProtection"/>): its chance is
    /// <paramref name="step"/> times the rolls since it last procced, counted for this key alone and from each fight's start.
    /// </summary>
    public bool RollBadLuckProc(SimulationContext ctx, string procKey, double step, EnemyTarget? target = null)
    {
        int misses = _badLuckMisses.GetValueOrDefault(procKey);
        bool procced = ctx.Outcomes.RollProc(procKey, Math.Min(1.0, (misses + 1) * step), ctx, target);
        _badLuckMisses[procKey] = procced ? 0 : misses + 1;
        return procced;
    }

    protected void ResetCombatState()
    {
        Resources.Reset();
        _badLuckMisses.Clear();
        foreach (var gem in Gems) gem.ResetLive();
        Auras.ClearAll();
        Auras.ClearTimers();
        QueueFlushingTick = null;
        Cooldowns.Clear();
        CooldownsVersion++;
        GlobalCooldownEndTime = SimTime.Zero;
        CurrentCast = null;
        CurrentChannel = null;
        if (ClassDamageTuning != 1.0)
        {
            Auras.Apply(AuraDef.Internal("Class tuning", 0, 1, StatModifier.More(StatType.DamageScale, ClassDamageTuning)), SimTime.Zero);
        }
        foreach (var passive in PassiveAuras) Auras.Apply(passive, SimTime.Zero);
        foreach (var situational in SituationalAuras) Auras.Apply(situational, SimTime.Zero);
    }
}

/// <summary>
/// Charges and recharge of one ability. A spent charge starts recharging immediately if none is recharging;
/// charges recharge one at a time. The timer holds cooldown-seconds and drains at a rate (1 = real time) that may
/// change while recharging (cooldown recovery, haste for charge abilities); the rate is re-read whenever
/// <see cref="Advance"/> is called. What the rate reads changes in events (an aura gained, removed or run out), and each
/// of them advances the hero's cooldowns.
/// </summary>
public sealed class CooldownState
{
    // Speed: the rate and its inputs are read from the hero directly, not through delegates (a read on nearly every Advance).
    private readonly Hero? _hero;
    private readonly Ability? _ability;
    private int _charges;
    private double _remaining;   // cooldown-seconds left on the charge recharging
    private double _rechargeBase;
    private SimTime _last;
    private double _rate = 1.0;
    // The version of the rate's inputs when it was last read (-1: never read).
    private long _rateReadAt = -1;
    // Set while the cooldown is full: no rate is read until a recharge starts (Spend). A new state is full, so it starts set.
    private bool _rateStale = true;
    private bool _advancing;

    /// <summary>A cooldown that recharges in real time.</summary>
    public CooldownState(int maxCharges)
    {
        MaxCharges = Math.Max(1, maxCharges);
        _charges = MaxCharges;
    }

    /// <summary>
    /// <paramref name="ability"/>'s cooldown, at <paramref name="hero"/>'s <see cref="Hero.CooldownRate"/>. While the
    /// hero's <see cref="Hero.CooldownRateInputs"/> is unchanged, the rate is not read again (the same rate).
    /// </summary>
    internal CooldownState(int maxCharges, Hero hero, Ability ability) : this(maxCharges) => (_hero, _ability) = (hero, ability);

    private void ReadRate(SimTime now)
    {
        if (_hero == null) return;
        long inputs = _hero.CooldownRateInputs(now);
        if (inputs == _rateReadAt) return;
        _rateReadAt = inputs;
        _rate = _hero.CooldownRate(_ability!, now);
    }

    public int MaxCharges { get; }
    private bool Recharging => _charges < MaxCharges;

    /// <summary>When the charge recharging now returns at the current rate (meaningful only while recharging).</summary>
    public SimTime NextChargeAt => _last + SimTime.FromSeconds(_remaining / Math.Max(1e-9, _rate));

    public int ChargesAt(SimTime now)
    {
        Advance(now);
        return _charges;
    }

    /// <summary>
    /// The part of the charge recharging that has come back by <paramref name="time"/> (0 to 1), at the rate read at
    /// <paramref name="now"/>: <paramref name="time"/> is now or later, with no event between. 0 when no charge recharges.
    /// </summary>
    public double RechargedPart(SimTime now, SimTime time)
    {
        Advance(now);
        if (!Recharging || _rechargeBase <= 0) return 0.0;
        return Math.Clamp(1.0 - Math.Max(0.0, (NextChargeAt - time).TotalSeconds) * _rate / _rechargeBase, 0.0, 1.0);
    }

    /// <summary>How much of a charge comes back each second at the rate read at <paramref name="now"/>; 0 when no charge recharges.</summary>
    public double RechargePerSecond(SimTime now)
    {
        Advance(now);
        return Recharging && _rechargeBase > 0 ? _rate / _rechargeBase : 0.0;
    }

    /// <summary>Spends a charge; a recharge of <paramref name="rechargeSeconds"/> cooldown-seconds starts if none is running.</summary>
    public void Spend(SimTime now, double rechargeSeconds)
    {
        Advance(now);
        if (_charges <= 0) return;
        bool wasFull = _charges == MaxCharges;
        _charges--;
        if (wasFull)
        {
            _rechargeBase = rechargeSeconds;
            _remaining = rechargeSeconds;
            // The rate as Advance(now) would have read it, had it been recharging.
            if (_rateStale && !_advancing)
            {
                _advancing = true;
                try { ReadRate(now); }
                finally { _advancing = false; }
            }
            _rateStale = false;
        }
    }

    /// <summary>
    /// Takes <paramref name="amount"/> cooldown-seconds off the charge recharging (a flat shave: worth fewer real seconds
    /// while recovery is fast).
    /// </summary>
    public void Reduce(SimTime amount, SimTime now)
    {
        Advance(now);
        if (!Recharging) return;
        _remaining -= amount.TotalSeconds;
        while (Recharging && _remaining <= 0)
        {
            _charges++;
            if (Recharging) _remaining += _rechargeBase;
        }
        if (!Recharging) _remaining = 0;
    }

    public void ResetAll()
    {
        _charges = MaxCharges;
        _remaining = 0;
        _rateStale = true;
    }

    /// <summary>Gives back one charge now (a forced cast in a replay); the recharge in progress carries on.</summary>
    public void GrantCharge(SimTime now)
    {
        Advance(now);
        if (_charges >= MaxCharges) return;
        _charges++;
        if (!Recharging) _remaining = 0;
    }

    /// <summary>Progresses the recharge to <paramref name="now"/> at the rate last read, then reads the rate at <paramref name="now"/>.</summary>
    public void Advance(SimTime now)
    {
        if (now < _last || _advancing) return;
        _advancing = true;
        try
        {
            var t = _last;
            while (t < now && Recharging)
            {
                double span = (now - t).TotalSeconds;
                double rate = Math.Max(1e-9, _rate);
                if (_remaining > span * rate)
                {
                    _remaining -= span * rate;
                    break;
                }
                // A charge returns before now.
                t += SimTime.FromSeconds(_remaining / rate);
                _charges++;
                _remaining = Recharging ? _rechargeBase : 0;
            }
            _last = now;
            // Speed: a full cooldown reads no rate; Spend reads it when a recharge starts.
            _rateStale = !Recharging;
            if (!_rateStale) ReadRate(now);
        }
        finally { _advancing = false; }
    }
}
