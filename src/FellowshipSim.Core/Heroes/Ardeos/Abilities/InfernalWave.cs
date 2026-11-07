namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1276: a cast that conjures a fiery wave toward the target, dealing Intellect fire damage and generating
/// Cinders. Talents that react to the wave's hit subscribe to <see cref="Ability.Hit"/>.
/// </summary>
public class InfernalWave : ArdeosAbility
{
    public const int AbilityId = 1276;
    public const string AbilityName = "Infernal Wave";
    public const double Coefficient = 2.0;
    public const double CastTimeSeconds = 1.5;
    public const double CindersGained = 40.0;

    /// <summary>Multipliers on the Cinders a cast generates, read as it resolves (Cascading Inferno doubles them).</summary>
    public List<Func<CastBonus, double>> CinderMultipliers { get; } = new();

    /// <summary>Whether the next cast is instant (Cascading Inferno).</summary>
    public List<Func<SimTime, bool>> InstantCastSources { get; } = new();

    public InfernalWave(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
    }

    public override SimTime GetAdjustedCastTime(SimTime now = default) =>
        InstantCastSources.Any(instant => instant(now)) ? SimTime.Zero : base.GetAdjustedCastTime(now);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        double cinders = CindersGained;
        if (CurrentCast != null)
        {
            foreach (var multiplier in CinderMultipliers) cinders *= multiplier(CurrentCast);
        }
        Ardeos.GainCinders(cinders, ctx, target);
        if (target != null) DealDamage(ctx, target);
    }
}
