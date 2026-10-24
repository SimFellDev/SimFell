namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1301: "Fire a magical arrow at target enemy, dealing magic damage to it." Instant, 15 Focus. "CELESTIAL
/// IMPETUS: Your Focused Shot hits have a chance to grant you Celestial Impetus for 15 seconds, causing your next Celestial
/// Shot to be Free and apply 3 stacks of Lunarlight Mark to the target. You can have up to 2 charges of Celestial Impetus
/// at any one time. (2.0 PPM)" Effect 1867 (15s, 2 stacks).
/// </summary>
public class CelestialShot : ElarionAbility
{
    public const int AbilityId = 1301;
    public const string AbilityName = "Celestial Shot";
    /// <summary>Placeholder CelestShot: 345.48% of Agility.</summary>
    public const double Coefficient = 3.4548;
    public const double Cost = 15.0;
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

    public CelestialShot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        FocusCost = Cost;
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
