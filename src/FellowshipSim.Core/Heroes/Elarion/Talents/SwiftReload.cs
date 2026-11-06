using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 682: The cooldown of Highwind Arrow is reduced each time it deals damage. Every hit counts,
/// the ricochets too.
/// </summary>
public class SwiftReload : Talent
{
    public const int TalentId = 682;
    public const string TalentName = "Swift Reload";
    public const double ReductionSeconds = 0.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        var reduction = SimTime.FromSeconds(ReductionSeconds);
        highwind.Hit += (in DamageContext hit) => hero.ReduceCooldown(highwind.Name, reduction, hit.Timestamp);
    }
}
