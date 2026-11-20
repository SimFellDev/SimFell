namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1201: Agility damage to the target enemy, increased for each combo point spent. Costs
/// Energy. During Matriarch Macabre shadowy clones repeat it (<see cref="MatriarchMacabre"/>).
/// </summary>
public partial class QueensFang : Finisher
{
    public QueensFang(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Constants.AgilityCoefficient;
        EnergyCost = AbilityCost;
    }

    /// <summary>
    /// Multipliers on the cast's own hit and not its clones', read and used up as the cast strikes (Feed the Queen).
    /// </summary>
    public List<Func<SimulationContext, double>> OwnHitMultipliers { get; } = new();

    protected override void Strike(SimulationContext ctx, EnemyTarget target, int points)
    {
        double multiplier = ComboPointMultiplier(points);
        // Read before the cast's own hit and its procs.
        var clones = Mara.Matriarch.KeepCast(this, ctx, multiplier * CastDamageMultiplier, CurrentCast?.CritChance ?? 0.0);
        double own = 1.0;
        foreach (var m in OwnHitMultipliers) own *= m(ctx);
        DealDamage(ctx, target, damageMultiplier: multiplier * own);
        Mara.Matriarch.Imitate(ctx, 1, (c, _) =>
        {
            if (!target.IsSleeping) DealKeptHit(c, target, Mara.Matriarch.StrikeNumbers(c, clones, target), MatriarchMacabre.CloneHitName(Name));
        });
    }

    /// <summary>A Queen's Fang struck by a shadowy clone of From the Shadows, with its own Combo Points.</summary>
    public void CloneStrike(SimulationContext ctx, EnemyTarget target, int points, double power) =>
        DealDamage(ctx, target, damageMultiplier: ComboPointMultiplier(points) * power, overrideName: MatriarchMacabre.CloneHitName(Name));
}
