namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2301: "Strike the target enemy with both axes in swift succession, dealing 82.5% of Strength per hit. Double
/// Strike can be used both in melee and at range. Applies Rend to the target for 50% of the damage dealt." Combat logs
/// (Y4wpb2f3gc8LqJDX fight 25): in melee both hits land with the cast; at range 0.24-0.30s and 0.44-0.50s after it.
/// </summary>
public class DoubleStrike : GundeAbility
{
    public const int AbilityId = 2301;
    public const string AbilityName = "Double Strike";
    public const double Coefficient = 0.825;
    public const int Hits = 2;
    public const double RendShareOfDamage = 0.5;

    public DoubleStrike(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        RendShare = RendShareOfDamage;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var rend = BeginRend(ctx);
        // Both hits are one pulse: Crimson Strikes empowers both.
        if (target is { IsSleeping: false }) RendingPulse(ctx, rend, Enumerable.Repeat(target, Hits).ToArray());
    }
}
