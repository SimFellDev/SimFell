using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Core.Heroes.Elarion;

/// <summary>
/// Elarion, the Skystrider. Focused Shot and passive regeneration build Focus; his other shots spend it. Lunarlight Mark
/// marks enemies, and damage to a marked enemy can trigger a Lunarlight Salvo. Abilities live in Abilities/, talents in
/// Talents/.
/// </summary>
public sealed class Elarion : Hero
{
    /// <summary>Hero 2 attributes MaxResources 100 and Resources 100: Elarion starts every fight with full Focus.</summary>
    public const double MaxFocus = 100;

    /// <summary>
    /// Focus regenerated per second before haste. The game data has the runtime formula Bowguy.ResourceGainPerInterval and
    /// no value. Combat logs (pWmcV42K39GHTLQX fight 23): with 5 per second raised by Haste the engine is within 1 Focus
    /// of the log over the first 40 seconds, at 29-71% Haste.
    /// </summary>
    public const double FocusPerSecond = 5.0;

    /// <summary>
    /// Seconds between two Focus regeneration ticks of the engine. A cast reads the Focus regenerated up to its moment
    /// (<see cref="RegenerateFocus"/>); the ticks let an action list waiting for Focus act again.
    /// </summary>
    public const double FocusTickSeconds = 0.1;

    public static readonly ResourceDef Focus = new("Focus", Max: MaxFocus, Initial: MaxFocus);

    /// <summary>Class tuning: hero 2 attribute BalancingDamageAura 1.08, +8%.</summary>
    public const double BalancingDamageAura = 1.08;

    /// <summary>Spirit Points per Spirit Refund (ability 1448, Bowguy.SpiritProc.ResourceRefund.FlatSpiritPointGain 1).</summary>
    public const double SpiritPointsPerRefund = 1.0;

    /// <summary>
    /// Hero 2's Spirit text: "Each time you trigger the Spirit Refund you ... apply 5 stacks of Lunarlight Mark to the
    /// target, plus 2 stacks to up to two additional nearby enemies."
    /// </summary>
    public const int RefundMarkStacks = 5;
    public const int RefundNearbyMarkStacks = 2;
    public const int RefundNearbyEnemies = 2;

    /// <summary>
    /// The choice of each nearby enemy a Spirit Refund marks: certain in a sim; a replay takes the enemies the log shows
    /// gaining Lunarlight Mark stacks at the refund.
    /// </summary>
    public const string RefundMarkTargetProcKey = "lunarlight_mark_refund_target";

    /// <summary>Elarion's talent tree, as in game (game data talent-slot records, hero Elarion).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                            b                              c
        TalentSlot.InRow(1, 2,    FocusedExpanse.TalentId,     PiercingSeekers.TalentId,      ResurgentWinds.TalentId),
        TalentSlot.InRow(2, 1,    DeadlyFocus.TalentId,        Fusillade.TalentId,            SkywardMunitions.TalentId),
        TalentSlot.InRow(3, 2,    RepeatingStars.TalentId,     LunarFury.TalentId,            LethalShots.TalentId),
        TalentSlot.InRow(4, 1,    RisingMoon.TalentId,         LunarlightAffinity.TalentId,   StrikersAim.TalentId),
        TalentSlot.InRow(5, 3,    FerventSupremacy.TalentId,   ImpendingHeartseeker.TalentId, FinalCrescendo.TalentId),
        TalentSlot.InRow(6, 1,    LastLights.TalentId,         SwiftReload.TalentId,          SkylitGrace.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Datamine hero 2: mainStat Agility.</summary>
    public override MainStat MainStat => MainStat.Agility;

    /// <summary>Multipliers on every Focus cost (Event Horizon), read as a cast pays.</summary>
    public List<Func<SimTime, double>> FocusCostMultipliers { get; } = new();

    /// <summary>Lunarlight Salvo: the damage a marked enemy takes, and what calls it down.</summary>
    public LunarlightSalvo Salvo { get; private set; } = null!;

    public override void Initialize()
    {
        AddResource(Focus);
        SpiritRefundResource = Focus;
        ClassDamageTuning = BalancingDamageAura;
        SpiritPointsPerSpiritRefund = SpiritPointsPerRefund;

        Salvo = new LunarlightSalvo(this);
        Register(LunarlightMark.Debuff);

        Abilities.Add(new Shoot(this));
        Abilities.Add(new FocusedShot(this));
        Abilities.Add(new CelestialShot(this));
        Abilities.Add(new Multishot(this));
        Abilities.Add(new HighwindArrow(this));
        Abilities.Add(new HeartseekerBarrage(this));
        Abilities.Add(new StarfallVolley(this));
        Abilities.Add(new LunarlightMark(this));
        Abilities.Add(new SkystridersGrace(this));
        Abilities.Add(new SkystridersSupremacy(this));
        Abilities.Add(new EventHorizon(this));

        OnSpiritRefund += (_, _, ctx) => MarkOnRefund(ctx);
        // Focus regenerates at the rate held since the last regeneration: haste is read again whenever an aura comes or goes.
        Auras.OnGained += (_, _, now) => RegenerateFocus(now);
        Auras.OnRemoved += (_, now) => RegenerateFocus(now);
    }

    /// <summary>A Spirit Refund marks the target with 5 stacks and up to two nearby enemies with 2.</summary>
    private void MarkOnRefund(SimulationContext ctx)
    {
        var target = CurrentTarget is { IsSleeping: false } current ? current : ctx.ActiveEnemies.FirstOrDefault();
        if (target == null) return;
        LunarlightMark.Mark(ctx, target, RefundMarkStacks);
        int chosen = 0;
        foreach (var enemy in ctx.ActiveEnemiesExcept(target))
        {
            if (chosen == RefundNearbyEnemies) break;
            if (!ctx.Outcomes.RollProc(RefundMarkTargetProcKey, 1.0, ctx, enemy)) continue;
            LunarlightMark.Mark(ctx, enemy, RefundNearbyMarkStacks);
            chosen++;
        }
    }

    // ------------------------------------------------------------------ Focus

    private SimTime _focusRegeneratedAt;
    private double _focusRate;

    /// <summary>Focus per second now: <see cref="FocusPerSecond"/> raised by haste ("The generation rate is affected by your haste").</summary>
    public double FocusRate(SimTime now) => FocusPerSecond * (1.0 + GetHaste(now));

    /// <summary>Adds the Focus regenerated since the last regeneration at the rate then, and reads the rate again.</summary>
    public void RegenerateFocus(SimTime now)
    {
        if (now <= _focusRegeneratedAt)
        {
            if (now == _focusRegeneratedAt) _focusRate = FocusRate(now);
            return;
        }
        double seconds = (now - _focusRegeneratedAt).TotalSeconds;
        _focusRegeneratedAt = now;
        var state = Resources.Get(Focus);
        state.Current = Math.Min(state.Max, state.Current + _focusRate * seconds);
        _focusRate = FocusRate(now);
    }

    public double FocusAt(SimTime now)
    {
        RegenerateFocus(now);
        return ResourceAmount(Focus);
    }

    /// <summary>The Focus cost multiplier now (Event Horizon).</summary>
    public double FocusCostMultiplier(SimTime now)
    {
        double multiplier = 1.0;
        foreach (var m in FocusCostMultipliers) multiplier *= m(now);
        return multiplier;
    }

    /// <summary>Spends Focus (a Spirit Refund may give it back).</summary>
    public void SpendFocus(double amount, SimulationContext ctx)
    {
        RegenerateFocus(ctx.CurrentTime);
        SpendResource(Focus, amount, ctx);
    }

    public void GainFocus(double amount, SimulationContext ctx, EnemyTarget? from = null)
    {
        RegenerateFocus(ctx.CurrentTime);
        GainResource(Focus, amount, ctx, from);
    }

    protected override void OnFightBegins(SimulationContext ctx)
    {
        _focusRegeneratedAt = ctx.CurrentTime;
        _focusRate = FocusRate(ctx.CurrentTime);
        void Tick(SimulationContext c)
        {
            RegenerateFocus(c.CurrentTime);
            c.Schedule(SimTime.FromSeconds(FocusTickSeconds), Tick, "Focus regeneration");
        }
        ctx.Schedule(SimTime.FromSeconds(FocusTickSeconds), Tick, "Focus regeneration");
    }

    public override void ResetForIteration()
    {
        ResetCombatState();
        _focusRegeneratedAt = SimTime.Zero;
        _focusRate = 0;
        foreach (var ability in Abilities.OfType<ElarionAbility>()) ability.ResetState();
    }
}
