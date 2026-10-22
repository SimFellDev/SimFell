namespace FellowshipSim.Core.Customization.Blessings;

/// <summary>
/// Blessing 710: "Your CORE abilities have a chance equal to your Critical Strike chance % to trigger The Usurper's Verdict
/// on the target, either dealing 143% / 228% / 365% / 584% Main Stat damage to up to 4 enemies near your target, or healing
/// up to 4 allies ... The Usurper's Verdict has a 20% chance to trigger both the Damage and Healing effect." (effects
/// 3190/3191). UNVERIFIED: no log has this blessing; the damage is taken to happen on every Verdict, under effect 3190.
/// </summary>
public class TheUsurper : Blessing
{
    public const int BlessingId = 710;
    public const string BlessingName = "The Usurper";
    public const int DamageEffectId = 3190;
    public const string DamageName = "The Usurper's Verdict";
    public const int MaxTargets = 4;
    public const string ProcKey = "the_usurper";
    public static readonly double[] CoefficientByRank = [1.43, 2.28, 3.65, 5.84];

    public override int Id => BlessingId;
    public override string Name => BlessingName;

    public override void Bind(Hero hero)
    {
        var verdict = new Verdict(hero, ForRank(CoefficientByRank));
        hero.OnCastCompleted += (ability, target, now) =>
        {
            if (ability.Category != AbilityCategory.Core || target == null || hero.SimContext is not { } ctx) return;
            if (!ctx.Outcomes.RollProc(ProcKey, Math.Clamp(hero.CritChance(now), 0.0, 1.0), ctx, target)) return;
            // The target first, then the others in order. Usually that is every active enemy as it stands.
            var active = ctx.ActiveEnemies;
            IReadOnlyList<EnemyTarget> nearby = active.Count <= MaxTargets && (active.Count == 0 || active[0] == target)
                ? active
                : active.OrderBy(e => e == target ? 0 : 1).Take(MaxTargets).ToList();
            verdict.DealAoeDamage(ctx, nearby);
        };
    }

    private sealed class Verdict : Ability
    {
        public Verdict(Hero owner, double coefficient) : base(owner, LogIds.Effect(DamageEffectId), DamageName)
        {
            BaseCoefficient = coefficient;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
