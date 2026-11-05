namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1201: "Deals 357.2% of Agility to target enemy. Damage increased for each combo point spent." Costs 40
/// Energy. During Matriarch Macabre two shadowy clones repeat it (<see cref="MatriarchMacabre"/>).
/// </summary>
public class QueensFang : Finisher
{
    public const int AbilityId = 1201;
    public const string AbilityName = "Queen's Fang";
    public const double Coefficient = 3.572;
    public const double Cost = 40;

    public QueensFang(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
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
