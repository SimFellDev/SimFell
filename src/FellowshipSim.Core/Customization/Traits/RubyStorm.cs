namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 35: "Your targeted, offensive abilities have a chance to spawn a Ruby Storm that travels forward, dealing damage
/// to all enemies it touches equal to 2.4% / 4.0% / 5.4% / 7.0% of your Current Health. (1.3 PPM)" (damage effect 90; it
/// scales with nothing else). UNVERIFIED: no log has this trait; the hero is taken to be at full health and the storm to
/// touch every enemy in combat.
/// </summary>
public class RubyStorm : Trait
{
    public const int TraitId = 35;
    public const string TraitName = "Ruby Storm";
    public const int DamageEffectId = 90;
    public const double ProcsPerMinute = 1.3;
    public const string ProcKey = "ruby_storm";
    public static readonly double[] HealthShareByRank = [0.024, 0.040, 0.054, 0.070];

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        double share = ForRank(HealthShareByRank);
        int logId = LogIds.Effect(DamageEffectId);
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        hero.OnCastCompleted += (ability, target, _) =>
        {
            if (target == null || ability.BaseCoefficient <= 0 || hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            double damage = share * hero.Stats.MaxHealth;
            foreach (var enemy in ctx.Outcomes.SelectTargets(logId, ctx.ActiveEnemies, ctx))
            {
                ctx.ApplyDamage(hero, enemy, damage, logId, TraitName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
