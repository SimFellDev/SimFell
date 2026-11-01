namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1845: "Strike your target, dealing physical damage to it as well as striking enemies near your target with 15%
/// of the power. Sun Strike applies Sunstruck to your main target for 30 seconds, stacking up to 10 times. ... Sun Strike
/// has a 15% chance to grant you 1 stack of Swift Reprieval."
/// </summary>
public class SunStrike : XavianAbility
{
    public const int AbilityId = 1845;
    public const string AbilityName = "Sun Strike";
    /// <summary>Placeholder SunstrikeDmg: 147% of Strength.</summary>
    public const double Coefficient = 1.47;
    /// <summary>Tooltip: enemies near the target take 15% of the power.</summary>
    public const double SplashFraction = 0.15;
    /// <summary>Game data targetCountFalloff: effect 2547 (Sun Strike), threshold 3.</summary>
    public const double SplashTargetThreshold = 3;

    /// <summary>Game data generates Resources 1, chance 0.15 (GA_Sune_SingleTargetFillerMelee @1649).</summary>
    public const double SwiftReprievalChance = 0.15;
    public const string ProcKey = "sun_strike_swift_reprieval";

    /// <summary>Effect 2664: 30 seconds, up to 10 stacks.</summary>
    public const int SunstruckEffectId = 2664;
    public const double SunstruckSeconds = 30.0;
    public const int SunstruckMaxStacks = 10;
    public static readonly AuraDef Sunstruck = AuraDef.Effect("Sunstruck", SunstruckEffectId, SunstruckSeconds, SunstruckMaxStacks);

    /// <summary>Sunstruck stacks one cast applies (Gleaming Strikes: 2).</summary>
    public int SunstruckStacks { get; set; } = 1;

    /// <summary>
    /// Raised as the cast resolves, before its hits (Sun's Verdict).
    /// </summary>
    public event Action<SimulationContext, EnemyTarget>? Striking;

    public SunStrike(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Striking?.Invoke(ctx, target);
        DealDamage(ctx, target);
        // Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 34.506): each nearby enemy's hit rolls its own crit, 15% of the main hit's
        // power, losing damage as sqrt(3/N) over the N nearby enemies.
        var nearby = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
        DealToEach(ctx, nearby, SplashTargetThreshold, SplashFraction);
        target.Auras.Apply(Sunstruck, ctx.CurrentTime, stacks: SunstruckStacks);
        Owner.TriggerHarmfulEffectApplied(ctx, target);
        if (ctx.Outcomes.RollProc(ProcKey, SwiftReprievalChance, ctx)) Xavian.GrantSwiftReprieval(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
