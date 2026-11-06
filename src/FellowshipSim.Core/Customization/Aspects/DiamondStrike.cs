using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 33: "Your Damage has a chance (6.7 PPM) to conjure a Spectral Beam from above, dealing 72% / 127% / 182% / 237%
/// Main Stat damage to them. ... Diamond Strike applies an amplifying effect for 20 seconds on targets it strikes,
/// increasing its Damage ... to that target by 40%, stacking up to 5 times. Diamond Strike gains an additional +35%
/// increased Damage ... for every stack of Harmonious Soul you have active (Diamond Gem Power)." (damage effect 96,
/// debuff 95). UNVERIFIED: no log has this aspect; the beam strikes at once and amplifies the strikes after it.
/// </summary>
public class DiamondStrike : Aspect
{
    public const int AspectId = 33;
    public const string AspectName = "Diamond Strike";
    public const int DamageEffectId = 96;
    public const int AmplifyEffectId = 95;
    public const double ProcsPerMinute = 6.7;
    public const string ProcKey = "diamond_strike";
    public const double AmplifySeconds = 20.0;
    public const int AmplifyMaxStacks = 5;
    public const double DamagePerAmplifyStack = 0.40;
    public const double DamagePerHarmoniousSoulStack = 0.35;
    public static readonly double[] CoefficientByRank = [0.72, 1.27, 1.82, 2.37];

    public static readonly AuraDef Amplified = AuraDef.Effect("Diamond Strike: Amplified", AmplifyEffectId, AmplifySeconds, AmplifyMaxStacks);

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        var beam = new Beam(hero, ForRank(CoefficientByRank));
        var rppm = new RealPpm(ProcKey, ProcsPerMinute, PpmScaling.Haste);
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target is not { } target || dmg.SimContext is not { } ctx || dmg.AbilityId == beam.DamageLogId) return;
            if (!rppm.Roll(hero, ctx, target)) return;
            var now = ctx.CurrentTime;
            int amplified = target.Auras.GetStacks(Amplified, now);
            var harmonious = hero.Gems.OfType<Diamond>().FirstOrDefault()?.HarmoniousSoul;
            int soul = harmonious == null ? 0 : hero.Auras.GetStacks(harmonious, now);
            beam.DealDamage(ctx, target, damageMultiplier: (1.0 + DamagePerAmplifyStack * amplified) * (1.0 + DamagePerHarmoniousSoulStack * soul));
            target.Auras.Apply(Amplified, now);
        };
    }

    private sealed class Beam : Ability
    {
        public Beam(Hero owner, double coefficient) : base(owner, LogIds.Effect(DamageEffectId), AspectName)
        {
            BaseCoefficient = coefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
