namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1841: "Instantly ascend to the skies in a beam of light and crash down at the targeted location, dealing
/// magical damage to enemies in the area. Can be used during Global Cooldown." Cooldown 60s, 2 charges.
/// </summary>
public class SkyCrash : XavianAbility
{
    public const int AbilityId = 1841;
    public const string AbilityName = "Sky Crash";
    /// <summary>Placeholder SkycrashDamage: 71% of Strength.</summary>
    public const double Coefficient = 0.71;
    public const double CooldownSeconds = 60.0;
    public const int Charges = 2;
    /// <summary>Game data targetCountFalloff: effect 2540, threshold 8; maxTargets 20.</summary>
    public const double TargetThreshold = 8;
    /// <summary>Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 10.051): the crash lands 0.17s after the cast. UNVERIFIED.</summary>
    public const double LandingSeconds = 0.17;

    /// <summary>Raised as Xavian lands (Tempered Descent, Zenith Technique).</summary>
    public event Action<SimulationContext>? Landed;

    public SkyCrash(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Movement)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, Charges);
        IgnoresGcd = true;
        AoeTargetThreshold = TargetThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        ctx.Schedule(SimTime.FromSeconds(LandingSeconds), c =>
        {
            if (c.ActiveEnemies.Count > 0) DealAoeDamage(c, c.ActiveEnemies);
            Landed?.Invoke(c);
        }, AbilityName);
    }
}
