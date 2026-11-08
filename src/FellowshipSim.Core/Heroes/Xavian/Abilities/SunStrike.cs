namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1845: "Strike your target, dealing physical damage to it as well as striking enemies near your target with 15%
/// of the power. Sun Strike applies Sunstruck to your main target for 30 seconds, stacking up to 10 times. ... Sun Strike
/// has a 15% chance to grant you 1 stack of Swift Reprieval."
/// </summary>
public partial class SunStrike : XavianAbility
{
    public const string ProcKey = "sun_strike_swift_reprieval";

    /// <summary>Effect 2664: 30 seconds, up to 10 stacks.</summary>
    public const int SunstruckEffectId = 2664;
    public const double SunstruckSeconds = 30.0;
    public const int SunstruckMaxStacks = 10;
    public static readonly AuraDef Sunstruck = AuraDef.Effect("Sunstruck", SunstruckEffectId, SunstruckSeconds, SunstruckMaxStacks);

    /// <summary>Sunstruck stacks a crit on the main target adds (Gleaming Strikes: 1).</summary>
    public int ExtraSunstruckOnCrit { get; set; }

    /// <summary>
    /// Raised as the cast resolves, before its hits (Sun's Verdict).
    /// </summary>
    public event Action<SimulationContext, EnemyTarget>? Striking;

    public SunStrike(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = DamageStrengthCoefficient;
        Hit += (in DamageContext hit) =>
        {
            if (hit.IsCrit && ExtraSunstruckOnCrit > 0 && hit.Target is { } struck && struck == _mainTarget)
            {
                struck.Auras.Apply(Sunstruck, hit.Timestamp, stacks: ExtraSunstruckOnCrit);
            }
        };
    }

    /// <summary>The cast's target while its main hit deals its damage.</summary>
    private EnemyTarget? _mainTarget;

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        if (target is not { IsSleeping: false }) return;
        Striking?.Invoke(ctx, target);
        _mainTarget = target;
        DealDamage(ctx, target);
        _mainTarget = null;
        // Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 34.506): each nearby enemy's hit rolls its own crit, 15% of the main hit's
        // power, losing damage as sqrt(3/N) over the N nearby enemies (log splash/main 0.130 at N = 4, 0.079 at 10, 0.057 at
        // 20; 0.15 x sqrt(3/N) is 0.130, 0.082 and 0.058). The main target shares the damage ID, so the splash leaves it out.
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
        var nearby = struck.Contains(target) ? struck.Where(e => e != target).ToList() : struck;
        DealToEach(ctx, nearby, CleaveTargetCountThreshold, CleaveDamageScaler);
        target.Auras.Apply(Sunstruck, ctx.CurrentTime, stacks: 1);
        Owner.TriggerHarmfulEffectApplied(ctx, target);
        if (ctx.Outcomes.RollProc(ProcKey, ResourceProcChance, ctx)) Xavian.GrantSwiftReprieval(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
