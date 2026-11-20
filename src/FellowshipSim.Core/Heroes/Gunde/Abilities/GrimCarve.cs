namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2262: "Throw your axe at the target enemy, where it spins like a sawblade, hitting all nearby enemies several
/// times and dealing damage with each spin. Applies Rend to the targets." Damage
/// falls off beyond <see cref="Constants.DamageScalingTargetCountThreshold"/> enemies (game data). The cooldown is shortened by Haste at the cast.
/// Combat logs (Y4wpb2f3gc8LqJDX fight 25, 40 casts): the spins land at fixed delays after the
/// cast, not hasted; each spin hits the enemies there as it lands.
/// </summary>
public partial class GrimCarve : GundeAbility
{
    public const int Spins = 3;
    /// <summary>The earliest usual times (combat logs), so a spin is never released after it should land.</summary>
    public const double FirstSpinSeconds = 0.167;
    public const double SpinIntervalSeconds = 0.467;

    /// <summary>Spins of each cast (Bloody Mess adds to them).</summary>
    public int SpinCount { get; set; } = Spins;

    /// <summary>Crit chance the cast's spins get, read and used up at the cast (Grim Harvest).</summary>
    public List<Func<SimulationContext, double>> CastCritChanceBonuses { get; } = new();

    public GrimCarve(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageStrengthCoefficientPerHit;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        AoeTargetThreshold = Constants.DamageScalingTargetCountThreshold;
        RendShare = Constants.PerTargetAccumulatedBleedDamageToDotScaler;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var rend = BeginRend(ctx);
        double crit = 0.0;
        foreach (var bonus in CastCritChanceBonuses) crit += bonus(ctx);
        for (int i = 0; i < SpinCount; i++)
        {
            ctx.Schedule(SimTime.FromSeconds(FirstSpinSeconds + i * SpinIntervalSeconds),
                c => RendingPulse(c, rend, AreaTargets(c, target), extraCritChance: crit > 0.0 ? _ => crit : null), Name);
        }
    }
}
