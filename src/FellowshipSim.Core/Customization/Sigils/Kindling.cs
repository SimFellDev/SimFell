namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 28: "Your damage effects apply a Damage over Time to the target, dealing fire damage equal to
/// 66% / 79% / 92% / 106% of your primary stat every 1.5 seconds over 9 seconds. (1.5 PPM)" (damage effect 104; the heal
/// over time does not affect damage). UNVERIFIED: no log has this sigil; the ticks are hasted like other damage over
/// time, and a new proc on the same enemy restarts it.
/// </summary>
public class Kindling : Sigil
{
    public const int SigilId = 28;
    public const string SigilName = "Kindling";
    public const int DamageEffectId = 104;
    public const double DurationSeconds = 9.0;
    public const double TickSeconds = 1.5;
    public const double ProcsPerMinute = 1.5;
    public const string ProcKey = "kindling";
    public static readonly double[] CoefficientByRank = [0.66, 0.79, 0.92, 1.06];

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var dot = new KindlingDot(hero, ForRank(CoefficientByRank));
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        var burning = new Dictionary<EnemyTarget, PeriodicEffect>();
        // The fight the burning enemies belong to (each sim iteration and replay has its own).
        SimulationContext? fight = null;
        void Ignite(SimulationContext ctx, EnemyTarget target)
        {
            if (burning.TryGetValue(target, out var old) && old.IsActive) old.Stop(ctx);
            burning[target] = PeriodicEffect.BeginHasted(ctx, hero, SigilName, DurationSeconds, TickSeconds,
                tickOnStart: false, partialFinalTick: false,
                onTick: (c, fraction) => { if (!target.IsSleeping) dot.DealDamage(c, target, damageMultiplier: fraction); },
                damageLogId: dot.DamageLogId, target: target);
        }

        // Speed: the handler captures nothing per hit; the burn's closure is made only when it ignites.
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target is not { } target || dmg.SimContext is not { } ctx || dmg.AbilityId == dot.DamageLogId) return;
            if (fight != ctx)
            {
                fight = ctx;
                burning.Clear();
            }
            if (rppm.Roll(hero, ctx, target)) Ignite(ctx, target);
        };
    }

    private sealed class KindlingDot : Ability
    {
        public KindlingDot(Hero owner, double coefficient) : base(owner, LogIds.Effect(DamageEffectId), SigilName)
        {
            BaseCoefficient = coefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
