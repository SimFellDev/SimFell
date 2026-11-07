using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 615 (ability 2064, damage effect 3175): "Your Apocalypse now additionally burns enemies for a share of the
/// damage it dealt. Your Apocalypse has extra Charges, and each time you cast Fire Ball the cooldown of your
/// Apocalypse ability is reduced." Item 5898 grants it to Ardeos: combat logs (1p7AXmHfwV9CYMxR fight 30,
/// dBgbn8wD1RxL7Kma fight 9) show Apocalypse: Explosivo damage from the Ardeos wearing it. The burn ticks periodically
/// (effect 3175) and adds to itself as Fire Ball's does.
/// </summary>
public class Explosivo : Legendary
{
    public const int PowerId = 615;
    public const int HelmItemId = 5898;
    public const int DamageEffectId = 3175;
    public const string DamageName = "Apocalypse: Explosivo";
    public const double FractionOfHit = 0.50;
    public const double PeriodSeconds = 3.0;
    public const double DurationSeconds = 12.0;
    public const int ExtraCharges = 1;
    public const double ReductionPerFireBallSeconds = 8.0;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Explosivo";
    public override string HeroName => "Ardeos";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Ardeos ardeos || ardeos.GetAbility<Apocalypse>() is not { } apocalypse) return;
        apocalypse.AddCharges(ExtraCharges);
        var burn = new Burn(ardeos);
        apocalypse.Hit += (in DamageContext hit) =>
        {
            if (hit.Amount > 0 && hit.Target != null && hit.SimContext != null)
            {
                ardeos.Dots.Apply(hit.SimContext, burn, hit.Target, pool: hit.Amount * FractionOfHit);
            }
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is FireBall) hero.ReduceCooldown(apocalypse.Name, SimTime.FromSeconds(ReductionPerFireBallSeconds), now);
        };
    }

    /// <summary>Apocalypse's burn: a share of the damage of its hit, dealt over the burn's duration.</summary>
    private sealed class Burn(Ardeos owner)
        : DamageOverTime(owner, DamageEffectId, DamageName, PeriodSeconds, DurationSeconds, DotStacking.Accumulate);
}
