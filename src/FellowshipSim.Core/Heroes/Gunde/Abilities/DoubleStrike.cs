namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2301: "Strike the target enemy with both axes in swift succession, dealing damage per hit. Double
/// Strike can be used both in melee and at range. Applies Rend to the target." Combat logs
/// (Y4wpb2f3gc8LqJDX fight 25): in melee both hits land with the cast; at range they land after it, each at its own delay.
/// </summary>
public partial class DoubleStrike : GundeAbility
{
    public const int Hits = 2;

    public DoubleStrike(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.DamageStrengthCoefficientPerHit;
        RendShare = Constants.PerTargetAccumulatedBleedDamageToDotScaler;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var rend = BeginRend(ctx);
        // Both hits are one pulse: Crimson Strikes empowers both.
        if (target is { IsSleeping: false }) RendingPulse(ctx, rend, Enumerable.Repeat(target, Hits).ToArray());
    }
}
