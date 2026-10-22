using FellowshipSim.Core.Customization.Blessings;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.Traits;
using FellowshipSim.Core.Customization.Weapons;

namespace FellowshipSim.Core.Domain;

public abstract class Hero : Actor
{
    protected Hero()
    {
        AddResource(SpiritPoints);
        // Spirit Points from damage: a hit earns its share of the enemy's health times the enemy's Spirit Point value.
        OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target is not { SpiritPointValue: > 0, MaxHealth: > 0 } enemy || dmg.Amount <= 0) return;
            GainResource(SpiritPoints, Math.Min(dmg.Amount, enemy.MaxHealth) / enemy.MaxHealth * enemy.SpiritPointValue, dmg.SimContext);
        };
        // Spirit of Heroism is applied by every hero's Spirit ability; register it up front so a replay compares it from the start.
        // Cooldown recovery depends on haste and cooldown acceleration: progress every recharge at the old rate when they change.
        // Speed: after an aura that cannot change a cooldown's rate, the recharges progress without reading the rate again.
        // They still progress at every aura change, so a recharge is subtracted in the same steps and rounds the same.
        Auras.OnGained += (aura, _, now) => AuraChanged(aura, now);
        Auras.OnRemoved += (aura, now) => AuraChanged(aura, now);
    }

    /// <summary>The stats <see cref="CooldownRate"/> reads (a weapon ability's own recovery auras are cooldown recovery too).</summary>
    private static readonly int CooldownRateStats =
        AuraDef.MaskOf(StatType.CooldownRecovery) | AuraDef.MaskOf(StatType.Haste) | AuraDef.MaskOf(StatType.HasteRating);

    private static readonly int CooldownRecoveryStat = AuraDef.MaskOf(StatType.CooldownRecovery);

    private void AuraChanged(Aura aura, SimTime now)
    {
        bool acceleration = CooldownAccelerationAuraKeys.Count > 0 && CooldownAccelerationAuraKeys.Contains(aura.Key);
        if (acceleration) _accelerationAuraChanges++;
        AdvanceCooldowns(now, readRate: acceleration || aura.Def.Changes(CooldownRateStats));
    }

    private void AdvanceCooldowns(SimTime now, bool readRate)
    {
        foreach (var state in Cooldowns.Values) state.Advance(now, readRate);
    }

    // Speed: CooldownRate reads only the auras' cooldown recovery and haste, the auras a cooldown acceleration reads (counted
    // as they change), the accelerations and the stat block. A recharge reads its rate again only after one of them changed;
    // the rate it would read is the rate it has.
    private (int Recovery, int Haste, int HasteRating, int Accelerations, int AccelerationAuras, StatBlock? Stats, int StatsVersion) _rateInputs;
    private long _rateInputsVersion;
    private int _accelerationAuraChanges;

    /// <summary>
    /// Changes whenever something <see cref="CooldownRate"/> reads may have changed. Expired auras are removed first, as the
    /// rate's own read of the auras does.
    /// </summary>
    private long CooldownRateInputs(SimTime now)
    {
        Auras.CleanExpired(now);
        var inputs = (Auras.StatVersion(StatType.CooldownRecovery), Auras.StatVersion(StatType.Haste), Auras.StatVersion(StatType.HasteRating),
            CooldownAccelerations.Count, _accelerationAuraChanges, Stats, Stats.Version);
        if (inputs != _rateInputs)
        {
            _rateInputs = inputs;
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
    public List<Blessing> Blessings { get; } = new();
    public List<Trait> Traits { get; } = new();
    public List<Legendary> Legendaries { get; } = new();
    public List<Gem> Gems { get; } = new();
    public List<Talent> Talents { get; } = new();
    public List<Customization.Sets.SetBonus> Sets { get; } = new();

    public Dictionary<string, CooldownState> Cooldowns { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Changes whenever a cooldown state is added to <see cref="Cooldowns"/> or they are cleared.</summary>
    public int CooldownsVersion { get; private set; }
    public SimTime GlobalCooldownEndTime { get; set; } = SimTime.Zero;

    /// <summary>The global cooldown before haste (per the user: 1.5s, hasted).</summary>
    public double GlobalCooldownSeconds { get; set; } = 1.5;

    /// <summary>Parts of the hero's build the engine does not model (filled when the build is imported).</summary>
    public List<string> UnmodeledFeatures { get; } = new();

    /// <summary>Permanent auras (passive talents, gear) applied at the start of every combat.</summary>
    public List<AuraDef> PassiveAuras { get; } = new();

    /// <summary>
    /// Auras that depend on what the engine does not simulate (your own health, standing still, fighting a boss). A sim
    /// keeps them up all fight; a replay applies and removes them when the log does.
    /// </summary>
    public List<AuraDef> SituationalAuras { get; } = new();

    /// <summary>
    /// The class tuning aura every hero has: a damage multiplier the game sets per hero (hero attribute
    /// BalancingDamageAura). 1.0 unless the hero sets its own value in <see cref="Initialize"/>.
    /// </summary>
    public double ClassDamageTuning { get; set; } = 1.0;

    /// <summary>The hero's talent tree, as in game.</summary>
    public abstract IReadOnlyList<TalentSlot> TalentTreeSlots { get; }

    /// <summary>
    /// Auras built at runtime (from a rank or a gem node) rather than declared as <c>public static readonly AuraDef</c>
    /// constants; see <see cref="Register"/>.
    /// </summary>
    public HashSet<AuraDef> RuntimeAuras { get; } = new();

    /// <summary>Extra crit chance against a specific target (e.g. bonuses vs. healthy enemies).</summary>
    public List<Func<EnemyTarget, SimTime, double>> TargetCritChanceBonuses { get; } = new();

    /// <summary>Damage multipliers against a specific target (e.g. bonuses vs. low-health enemies).</summary>
    public List<Func<EnemyTarget, SimTime, double>> TargetDamageMultipliers { get; } = new();

    /// <summary>Damage multipliers for specific abilities that depend on the moment (e.g. scaling with current Spirit).</summary>
    public List<Func<Ability, SimTime, double>> AbilityDamageMultipliers { get; } = new();

    /// <summary>Cooldown acceleration for specific abilities (0.05 = cooldown recovers 5% faster).</summary>
    public List<Func<Ability, SimTime, double>> CooldownAccelerations { get; } = new();

    /// <summary>Keys of the auras a <see cref="CooldownAccelerations"/> entry reads, other than through haste (e.g. Inspired Arms).</summary>
    public HashSet<string> CooldownAccelerationAuraKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Seconds added to Spirit of Heroism.</summary>
    public double SpiritOfHeroismDurationBonus { get; set; }

    /// <summary>Auras active exactly while Spirit of Heroism is active.</summary>
    public List<AuraDef> SpiritOfHeroismAuras { get; } = new();

    /// <summary>Spirit of Heroism (effect 2253, granted by every hero's Spirit ability): +30% Haste.</summary>
    public const int SpiritOfHeroismEffectId = 2253;
    public const double SpiritOfHeroismSeconds = 20.0;
    public const double SpiritOfHeroismHaste = 0.3;

    /// <summary>Spirit of Heroism: every hero's Spirit ability grants +30% Haste for 20 seconds.</summary>
    public static readonly AuraDef SpiritOfHeroism = AuraDef.Effect("Spirit of Heroism", SpiritOfHeroismEffectId, SpiritOfHeroismSeconds,
        modifiers: StatModifier.Add(StatType.Haste, SpiritOfHeroismHaste));

    /// <summary>A hit of the hero landed. Handlers run in the order they subscribed, with those of <see cref="OnDamageDealtBy"/> and <see cref="Ability.Hit"/>.</summary>
    public event DamageDealtHandler? OnDamageDealt
    {
        add { if (value != null) Subscribe(new DamageDealtSubscriber(null, null, value)); }
        remove
        {
            int i = _damageDealt.FindLastIndex(s => s.Ability == null && s.DamageId == null && s.Handler == value);
            if (i < 0) return;
            _damageDealt.RemoveAt(i);
            _damageDealtById.Clear();
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

    private void Subscribe(DamageDealtSubscriber subscriber)
    {
        _damageDealt.Add(subscriber);
        _damageDealtById.Clear();
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
    /// <summary>Spirit of Heroism started / ended.</summary>
    public event Action<SimulationContext>? SpiritOfHeroismStarted;
    public event Action<SimulationContext>? SpiritOfHeroismEnded;
    /// <summary>A Spirit Refund triggered on a spend of the resource (the refund itself arrives a moment later).</summary>
    public event Action<ResourceDef, double, SimulationContext>? OnSpiritRefund;
    /// <summary>A cast is about to resolve (also free casts from procs): talents add to it here (see <see cref="CastBonus"/>).</summary>
    public event Action<CastBonus>? OnCastResolving;

    /// <summary>The cast in progress, if any (abilities with a cast time between begin and completion).</summary>
    public ActiveCast? CurrentCast { get; internal set; }

    /// <summary>The channel in progress, if any.</summary>
    public ActiveChannel? CurrentChannel { get; internal set; }

    public void TriggerDamageDealt(in DamageContext hit)
    {
        if (!_damageDealtById.TryGetValue(hit.AbilityId, out var subscribers))
        {
            int id = hit.AbilityId;
            subscribers = _damageDealt.Where(s => s.Receives(id)).ToArray();
            _damageDealtById[id] = subscribers;
        }
        foreach (var subscriber in subscribers)
        {
            if (subscriber.Ability != null) subscriber.Ability.RaiseHit(in hit);
            else subscriber.Handler!(in hit);
        }
    }
    public void TriggerCastStarted(Ability a, EnemyTarget? t, SimTime now = default) => OnCastStarted?.Invoke(a, t, now);
    public void TriggerCastCompleted(Ability a, EnemyTarget? t, SimTime now = default) => OnCastCompleted?.Invoke(a, t, now);
    public void TriggerCastCancelled(Ability a, EnemyTarget? t, SimTime now = default) => OnCastCancelled?.Invoke(a, t, now);
    public void TriggerChannelEnded(Ability a, EnemyTarget? t, SimTime now, bool clipped) => OnChannelEnded?.Invoke(a, t, now, clipped);
    public void TriggerChannelTick(Ability a, EnemyTarget? t, double fraction, SimulationContext ctx) => OnChannelTick?.Invoke(a, t, fraction, ctx);

    // ------------------------------------------------------------------ resources and Spirit Refund

    /// <summary>Spirit Points toward the Spirit ability (every hero).</summary>
    /// <summary>
    /// Spirit Points: they fill the Spirit ability (hero_data Attributes MaxSpiritPoints 100). Every hero regains 1 every
    /// 3 seconds in combat and earns enemies' Spirit Points through damage (combat logs report them as resource type 4).
    /// </summary>
    public static readonly ResourceDef SpiritPoints = new("Spirit", Max: 100);
    public const double SpiritPointRegenSeconds = 3.0;
    public const double SpiritPointRegenAmount = 1.0;
    /// <summary>The Spirit ability's cost (hero_data Ultimate SpiritPointCost 100).</summary>
    public const double SpiritAbilityBaseCost = 100.0;

    /// <summary>Spirit Points a Spirit Refund grants (hero_data SpiritProc FlatSpiritPointGain: 1, Rime 2).</summary>
    public double SpiritPointsPerSpiritRefund { get; protected set; } = 1.0;

    /// <summary>Spirit Points at the start of a dungeon (The Herald, Undulating Spirit...).</summary>
    public double StartingSpiritPoints { get; set; }

    /// <summary>Share taken off the Spirit ability's cost (Blessing of the Prophet).</summary>
    public double SpiritAbilityCostReduction { get; set; }

    public double SpiritAbilityCost => SpiritAbilityBaseCost * (1.0 - SpiritAbilityCostReduction);

    /// <summary>Raises the maximum Spirit Points (Ancestral Surge).</summary>
    public void AddMaxSpiritPoints(double amount) => Resources.Get(SpiritPoints).Max += amount;

    /// <summary>Starts what runs for the whole fight on its own: the Spirit Point regeneration.</summary>
    public void BeginFight(SimulationContext ctx)
    {
        void Regen(SimulationContext c)
        {
            GainResource(SpiritPoints, SpiritPointRegenAmount, c);
            c.Schedule(SimTime.FromSeconds(SpiritPointRegenSeconds), Regen, "Spirit Point regeneration");
        }
        ctx.Schedule(SimTime.FromSeconds(SpiritPointRegenSeconds), Regen, "Spirit Point regeneration");
    }

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
    public CastBonus PrepareCast(Ability ability, EnemyTarget? target, SimTime now, object? source = null)
    {
        var cast = new CastBonus(ability, target, now, source);
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
        if (!RollSpiritRefund(ctx)) return;
        // Rising Spirit comes with the spend; the resource comes back a moment later.
        Auras.Apply(RisingSpirit, ctx.CurrentTime);
        OnSpiritRefund?.Invoke(resource, amount, ctx);
        GainResource(SpiritPoints, SpiritPointsPerSpiritRefund, ctx);
        ctx.Schedule(SimTime.FromSeconds(SpiritRefundDelaySeconds), c => AddToResource(resource, amount, c, null, isGain: false), "Spirit Refund");
    }

    /// <summary>Tells effects that count spent resource (e.g. cooldown reductions per point spent) that it was spent.</summary>
    public void NotifyResourceSpent(ResourceDef resource, double amount, SimTime now) => OnResourceSpent?.Invoke(resource, amount, now);

    /// <summary>The resource a Spirit Refund gives back: the hero's spender resource (e.g. Rime's Winter Orbs).</summary>
    public ResourceDef? SpiritRefundResource { get; protected set; }

    public const string SpiritRefundProc = "spirit_refund";
    /// <summary>Combat logs (10 Rime logs): refunded resource shows up again 0.233s (10th percentile) after the spend.</summary>
    public const double SpiritRefundDelaySeconds = 0.2;

    /// <summary>Rising Spirit (effect 3115): +5% primary stat per stack for 10s, up to 10 stacks, from Spirit Refunds.</summary>
    public const int RisingSpiritEffectId = 3115;
    public const double RisingSpiritSeconds = 10.0;
    public const int RisingSpiritMaxStacks = 10;
    public const double RisingSpiritPrimaryStatPerStack = 0.05;
    /// <summary>Combat logs (10 Rime logs): each further Spirit Refund adds 2s to the time left instead of restarting it.</summary>
    public const double RisingSpiritReapplySeconds = 2.0;

    public static readonly AuraDef RisingSpirit = AuraDef.Effect("Rising Spirit", RisingSpiritEffectId, RisingSpiritSeconds,
        RisingSpiritMaxStacks, StatModifier.Add(StatType.PrimaryAttributeMultiplier, RisingSpiritPrimaryStatPerStack))
        with { ReapplyExtendsSeconds = RisingSpiritReapplySeconds };

    /// <summary>Effects that make a Spirit Refund certain (e.g. Undulating Spirit): each returns true if it guaranteed this one.</summary>
    public List<Func<SimTime, bool>> GuaranteedSpiritRefunds { get; } = new();

    private bool RollSpiritRefund(SimulationContext ctx)
    {
        foreach (var guaranteed in GuaranteedSpiritRefunds)
        {
            if (guaranteed(ctx.CurrentTime)) return true;
        }
        // Spirit Refund chance is Spirit / (1 + Spirit) (per the user: "%/(100+%)").
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
        var heroism = SpiritOfHeroism.WithDuration(SpiritOfHeroismSeconds + SpiritOfHeroismDurationBonus);
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
    /// How fast <paramref name="ability"/>'s cooldown ticks down (1 = real time). Recovery sources share one pool: each
    /// adds its bonus part (Chronoshift's 8x adds 7, Strand of Eternity's 1.1x adds 0.1, The Monarch its percentage).
    /// Charge abilities that recharge with haste add the haste on top. Weapon abilities ignore the pool except for their own
    /// effects: Chronoshift recovers at 8x while channeling it (per the user; 10 Rime logs: no early casts, and ruru's
    /// fastest comes back 1.1s before it is pressed, against 7.7s if weapons took the whole pool).
    /// (Fellowship Theorycrafting, "How Cooldowns Actually Work", measured from live logs.)
    /// </summary>
    public double CooldownRate(Ability ability, SimTime now)
    {
        bool weaponOwnOnly = ability.IgnoresCooldownRecovery;
        double pool = 1.0 + (weaponOwnOnly ? 0.0 : CooldownAcceleration(ability, now));
        if (!weaponOwnOnly)
        {
            pool += Auras.SumBonuses(StatType.CooldownRecovery, now);
        }
        else
        {
            foreach (var aura in Auras.Active(now))
            {
                // Weapon abilities only speed up from their own recovery effects (Chronoshift's barrier, per the user).
                if (!aura.Def.Changes(CooldownRecoveryStat) || !ability.OwnRecoveryAuraLogIds.Contains(aura.Def.LogId)) continue;
                var mods = aura.Modifiers;
                for (int i = 0; i < mods.Count; i++)
                {
                    if (mods[i].Type == StatType.CooldownRecovery) pool += mods[i].Value - 1.0;
                }
            }
        }
        return pool + (ability.ChargesRechargeWithHaste ? GetHaste(now) : 0.0);
    }

    /// <summary>Recovery bonuses specific to an ability (e.g. The Monarch for Major abilities), as fractions.</summary>
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

    /// <summary>Total haste at <paramref name="now"/>: rating haste (with rating buffs) plus haste from auras.</summary>
    public double GetHaste(SimTime now) =>
        IsCurrent(ref _haste, StatType.HasteRating, StatType.Haste, now)
            ? _haste.Value
            : _haste.Value = Math.Max(0.0,
                StatBlock.WithExtraRating(Stats.HastePercent, Stats.HasteRating, Auras.Fold(StatType.HasteRating, 0.0, now))
                + Auras.Fold(StatType.Haste, 0.0, now));

    public double Expertise(SimTime now) =>
        IsCurrent(ref _expertise, StatType.ExpertiseRating, StatType.Expertise, now)
            ? _expertise.Value
            : _expertise.Value = StatBlock.WithExtraRating(Stats.ExpertisePercent, Stats.ExpertiseRating, Auras.Fold(StatType.ExpertiseRating, 0.0, now))
                                 + Auras.Fold(StatType.Expertise, 0.0, now);

    /// <summary>Spirit as a fraction (0.30 = 30% Spirit).</summary>
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

    // Its own method: the rate's closure over the ability is made only for a new state, not on every CooldownOf.
    private CooldownState NewCooldownState(Ability ability) =>
        new(ability.MaxCharges, rateAt: now => CooldownRate(ability, now), rateInputs: _rateInputsReader ??= CooldownRateInputs);

    private Func<SimTime, long>? _rateInputsReader;

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


    protected void ResetCombatState()
    {
        Resources.Get(SpiritPoints).Initial = StartingSpiritPoints;
        Resources.Reset();
        Auras.ClearAll();
        Auras.ClearTimers();
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
    private readonly Func<SimTime, double> _rateAt;
    private readonly Func<SimTime, long>? _rateInputs;
    private int _charges;
    private double _remaining;   // cooldown-seconds left on the charge recharging
    private double _rechargeBase;
    private SimTime _last;
    private double _rate = 1.0;
    // The version of the rate's inputs when it was last read (-1: never read).
    private long _rateReadAt = -1;
    // Set when Advance skipped reading the rate of a full cooldown.
    private bool _rateStale;
    private bool _advancing;

    /// <param name="rateAt">The rate at a time.</param>
    /// <param name="rateInputs">
    /// A version of what the rate reads, at a time: while it is unchanged, the rate is not read again (the same rate).
    /// </param>
    public CooldownState(int maxCharges, Func<SimTime, double>? rateAt = null, Func<SimTime, long>? rateInputs = null)
    {
        MaxCharges = Math.Max(1, maxCharges);
        _charges = MaxCharges;
        _rateAt = rateAt ?? (_ => 1.0);
        _rateInputs = rateInputs;
    }

    private void ReadRate(SimTime now)
    {
        if (_rateInputs != null)
        {
            long inputs = _rateInputs(now);
            if (inputs == _rateReadAt) return;
            _rateReadAt = inputs;
        }
        _rate = _rateAt(now);
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
    public void Advance(SimTime now) => Advance(now, readRate: true);

    /// <summary>
    /// <see cref="Advance(SimTime)"/>. With <paramref name="readRate"/> false the rate is not read at
    /// <paramref name="now"/>: nothing the rate depends on has changed since it was last read.
    /// </summary>
    internal void Advance(SimTime now, bool readRate)
    {
        if (now == default || now < _last || _advancing) return;
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
            if (!_rateStale && readRate) ReadRate(now);
        }
        finally { _advancing = false; }
    }
}
