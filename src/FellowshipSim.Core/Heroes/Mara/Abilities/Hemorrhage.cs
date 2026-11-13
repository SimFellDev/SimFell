namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1724: Hemorrhaging Strike's bleed, dealing Agility damage on a period; each tick of the bleed replenishes
/// Energy. Creeping Death adds damage per Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 32 bleeds): ticks are shortened by Haste, with a partial tick as it runs out;
/// a refresh keeps the tick timing, and up to <see cref="RefreshCarryover"/> of the new duration of the time left carries over (@ 21.458).
/// Every enemy's bleed pays Energy at each tick, a partial one too, with Maiden of Death's
/// multiplier: game data reads no partial tick factor for the Energy, and
/// the signal to the source ability runs in the partial tick's executions.
/// The period is <see cref="TickSeconds"/> / Haste times the application's tick period scaler (`SetByCaller.System.TickRateOverride`).
/// Game data (build 26320967): a refresh replaces the spec, so the scaler reads 1, and the period timer runs on
/// (`StackPeriodResetPolicy` NeverReset, `ApplyGameplayEffectSpec` exe 0x144A6A660); each write of the scaler
/// (`SetGameplayEffectCustomTickPeriodScaler` exe 0x145243620) rates the running tick again with its fraction kept (0x1452DC7E0).
/// The writes come in this order: the applied-to-target listeners of the application (<see cref="ApplicationPeriodMultipliers"/>),
/// then the applying ability's own (<see cref="AbilityPeriodMultipliers"/>), on the bleed the application returned when it is
/// still on its enemy (`GA_Mara_BleedAttackSpender` offsets 1612, 1752 and 2209).
/// </summary>
public class Hemorrhage : MaraDot
{
    public const int EffectId = 1724;
    public const string EffectName = "Hemorrhage";
    public const double Coefficient = 0.9841;
    public const double TickSeconds = 3.0;
    public const double BaseDurationSeconds = 12.0;
    public const double EnergyPerTick = 2.0;
    public const double RefreshCarryover = 0.3;

    public Hemorrhage(Mara owner) : base(owner, EffectId, EffectName, Coefficient, TickSeconds, BaseDurationSeconds)
    {
        HasCreepingDeath = true;
        RefreshCarryoverFraction = RefreshCarryover;
    }

    /// <summary>
    /// Multipliers of the tick period scaler at each application of the bleed to an enemy: a fresh one, a refresh and each
    /// copy of a re-application (From the Shadows).
    /// </summary>
    public List<double> ApplicationPeriodMultipliers { get; } = new();

    /// <summary>
    /// Multipliers of the tick period scaler the applying ability writes after the application's listeners (Bloodrush):
    /// Hemorrhaging Strike on its bleed, and a re-application on each copy.
    /// </summary>
    public List<double> AbilityPeriodMultipliers { get; } = new();

    /// <summary>
    /// The enemies besides the struck one that a re-application of the struck enemy's bleed copies it to, or null for no
    /// re-application (Gushing Blood during Maiden of Death).
    /// </summary>
    public Func<SimulationContext, EnemyTarget, IEnumerable<EnemyTarget>?>? ReapplicationTargets { get; set; }

    /// <summary>
    /// Hemorrhaging Strike's bleed on <paramref name="target"/> for <paramref name="durationSeconds"/>: the application, then a
    /// re-application (<see cref="ReapplicationTargets"/>) or the writes of the tick period scaler.
    /// </summary>
    public void ApplyFromStrike(SimulationContext ctx, EnemyTarget target, double durationSeconds)
    {
        var dot = Apply(ctx, target, durationSeconds);
        if (ReapplicationTargets?.Invoke(ctx, target) is { } others)
        {
            Reapply(ctx, dot, others.ToList());
            return;
        }
        WritePeriodScaler(dot);
    }

    /// <summary>The application's listeners' writes of the tick period scaler, then the applying ability's.</summary>
    private void WritePeriodScaler(Instance dot)
    {
        foreach (double m in ApplicationPeriodMultipliers) ScalePeriod(dot, m);
        foreach (double m in AbilityPeriodMultipliers) ScalePeriod(dot, m);
    }

    /// <summary>
    /// Takes the struck enemy's bleed off and applies a copy of it afresh to <paramref name="others"/>, then to the struck
    /// enemy: each copy has the bleed's duration left, takes Mara's bleed off its enemy first, gets the writes of the tick period
    /// scaler on its own application, and has its first tick one period after it. Game data (build 26320967), ability 1517
    /// (`GA_Mara_Passive_Talent_ChargedResourceRefillSelfBuff_BleedAttackSpenderBleedAoe`): offset 2558 removes the struck
    /// enemy's bleed, 2629 clones the live spec (the refreshed duration), 1406 removes Mara's bleed from each target, 1600 applies
    /// the clone, and 1666 to 2197 write Bloodrush's multiplier on each copy. A removal by these routes has no partial tick
    /// (`CheckDuration` exe 0x144A6FAB0 alone gives one). The applied-to-target listeners run the last registered first (exe
    /// 0x144A00010), and the order of ability 1517's listener and the other listeners of the application is unread: the engine
    /// runs ability 1517's first, so each copy starts at scaler 1, and a listener after it finds the struck enemy's bleed removed.
    /// </summary>
    private void Reapply(SimulationContext ctx, Instance source, IReadOnlyList<EnemyTarget> others)
    {
        var struck = source.Target;
        double duration = Math.Max(0.0, (source.End - ctx.CurrentTime).TotalSeconds);
        Remove(ctx, struck, expired: false);
        foreach (var enemy in others.Where(e => e != struck).Append(struck))
        {
            Remove(ctx, enemy, expired: false);
            WritePeriodScaler(Apply(ctx, enemy, duration));
        }
    }

    protected override void DealTick(SimulationContext ctx, Instance dot, double fraction)
    {
        Mara.GainEnergyWithMaiden(EnergyPerTick, ctx);
        base.DealTick(ctx, dot, fraction);
    }

    /// <summary>The damage of one full tick on <paramref name="target"/> now, before crit (Hemotoxin).</summary>
    public double TickDamage(SimulationContext ctx, EnemyTarget target) => BaseDamage(ctx, target);

    /// <summary>
    /// Ticks in <paramref name="durationSeconds"/> at the tick rate now of a bleed Hemorrhaging Strike applies with no
    /// re-application (Hemotoxin).
    /// </summary>
    public double TicksIn(double durationSeconds, SimTime now)
    {
        double scaler = 1.0;
        foreach (double m in ApplicationPeriodMultipliers.Concat(AbilityPeriodMultipliers)) scaler *= m;
        return durationSeconds * (1.0 + Owner.GetHaste(now)) / (PeriodSeconds * scaler);
    }
}
