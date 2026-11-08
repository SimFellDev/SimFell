namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 975: "Instantly resets the cooldown of your Shield Slam ability and causes your next Shield Slam within 12 seconds
/// to be Empowered. Empowered Shield Slam: Your next Shield Slam applies a barrier on you, absorbing up to 1743% of Strength
/// damage taken for 8 seconds. Can be used during Global Cooldown. VETERAN OF WAR: Hold the Line reduces the cooldown of
/// your Shield Slam, Shield Throw, Shields Up, and Shockwave abilities by 10 seconds." Cooldown 30s. Empowered Shield Slam
/// is effect 2435 (up to 2 stacks) and its barrier effect 2229.
/// </summary>
public partial class HoldTheLine : HelenaAbility
{
    public const double ReductionSeconds = 10.0;
    public const int EmpoweredEffectId = 2435;
    public const double EmpoweredSeconds = 12.0;
    public const int EmpoweredMaxStacks = 2;
    public const int BarrierEffectId = 2229;
    public const double BarrierSeconds = 8.0;

    /// <summary>Empowered Shield Slam: the next Shield Slam applies <see cref="Barrier"/>.</summary>
    public static readonly AuraDef Empowered = AuraDef.Effect("Empowered Shield Slam", EmpoweredEffectId, EmpoweredSeconds, EmpoweredMaxStacks);

    /// <summary>The barrier an Empowered Shield Slam applies on Helena, for 8 seconds.</summary>
    public static readonly AuraDef Barrier = AuraDef.Effect("Empowered Shield Slam: Barrier", BarrierEffectId, BarrierSeconds);

    public HoldTheLine(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        VeteranOfWarReductions.Add((typeof(ShieldSlam), ReductionSeconds));
        VeteranOfWarReductions.Add((typeof(ShieldThrow), ReductionSeconds));
        VeteranOfWarReductions.Add((typeof(ShieldsUp), ReductionSeconds));
        VeteranOfWarReductions.Add((typeof(Shockwave), ReductionSeconds));
    }

    /// <summary>Iron Sentinel: 2 charges, each recharging with Haste.</summary>
    public void UseChargesWithHaste(int charges) => SetCooldown(AbilityCooldown, charges, scalesWithHaste: true);

    public override void Connect()
    {
        // The next Shield Slam uses up a stack of Empowered Shield Slam and applies the barrier.
        if (Helena.GetAbility<ShieldSlam>() is { } shieldSlam)
        {
            shieldSlam.Slammed += ctx =>
            {
                if (!Helena.Auras.HasAura(Empowered, ctx.CurrentTime)) return;
                Helena.Auras.ConsumeStack(Empowered, ctx.CurrentTime);
                Helena.Auras.Apply(Barrier, ctx.CurrentTime);
            };
        }
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (Helena.GetAbility<ShieldSlam>() is { } shieldSlam) Helena.CooldownOf(shieldSlam).ResetAll();
        Helena.Auras.Apply(Empowered, ctx.CurrentTime);
        ApplyVeteranOfWar(ctx);
    }
}
