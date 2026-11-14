namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1301: "Fire a magical arrow at target enemy, dealing magic damage to it." Instant, costs Focus. Celestial
/// Impetus: Focused Shot hits have a chance to grant Celestial Impetus, which makes the next Celestial Shot Free and applies
/// Lunarlight Mark stacks to the target. Effect 1867 holds the charges.
/// </summary>
public partial class CelestialShot : ElarionAbility
{
    public const int ImpetusEffectId = 1867;
    public const double ImpetusSeconds = 15.0;
    public const int ImpetusMaxStacks = 2;
    public const double ImpetusProcsPerMinute = 2.0;
    public const int ImpetusMarkStacks = 3;
    public const string ImpetusProcKey = "celestial_impetus";

    public static readonly AuraDef Impetus = AuraDef.Effect("Celestial Impetus", ImpetusEffectId, ImpetusSeconds, ImpetusMaxStacks);

    /// <summary>A Celestial Shot used up Celestial Impetus (Impending Heartseeker).</summary>
    public event Action<SimulationContext, EnemyTarget>? ImpetusConsumed;

    private readonly RealPpm _impetus = new(ImpetusProcKey, ImpetusProcsPerMinute);

    public CelestialShot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.AgilityCoefficient;
        FocusCost = Constants.Cost;
    }

    public override void Connect()
    {
        var focusedShot = Elarion.GetAbility<FocusedShot>();
        if (focusedShot == null) return;
        focusedShot.Landed += (ctx, _) =>
        {
            if (_impetus.Roll(Owner, ctx)) Owner.Auras.Apply(Impetus, ctx.CurrentTime);
        };
    }

    public override double CostAt(SimTime now) => Owner.Auras.HasAura(Impetus, now) ? 0.0 : base.CostAt(now);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        bool impetus = Owner.Auras.HasAura(Impetus, now);
        if (impetus)
        {
            Owner.Auras.ConsumeStack(Impetus, now);
            LunarlightMark.Mark(ctx, target, ImpetusMarkStacks);
            ImpetusConsumed?.Invoke(ctx, target);
        }
        else
        {
            Pay(ctx, CostAt(now));
        }
        DealDamage(ctx, target);
    }
}
