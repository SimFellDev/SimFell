namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 982: "Perform a heavy strike, dealing 403.4% of Strength physical damage to target. VETERAN OF WAR: Power Strike
/// reduces the cooldown of your Shield Slam and Shield Throw abilities by 2 seconds." Cooldown 6s, shortened by Haste at
/// the cast.
/// </summary>
public partial class PowerStrike : HelenaAbility
{
    public const double ReductionSeconds = 2.0;

    /// <summary>Multipliers on the hit against its enemy (Sharpened Blade).</summary>
    public List<Func<SimulationContext, EnemyTarget, double>> TargetMultipliers { get; } = new();

    /// <summary>Raised as the cast resolves (Sword &amp; Board).</summary>
    public event Action<SimulationContext>? Struck;

    public PowerStrike(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        VeteranOfWarReductions.Add((typeof(ShieldSlam), ReductionSeconds));
        VeteranOfWarReductions.Add((typeof(ShieldThrow), ReductionSeconds));
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false })
        {
            double multiplier = 1.0;
            foreach (var bonus in TargetMultipliers) multiplier *= bonus(ctx, target);
            DealDamage(ctx, target, damageMultiplier: multiplier);
        }
        ApplyVeteranOfWar(ctx);
        Struck?.Invoke(ctx);
    }
}
