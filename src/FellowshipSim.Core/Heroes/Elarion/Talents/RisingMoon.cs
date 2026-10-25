using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>Talent 681: "The cooldown of Lunarlight Mark is reduced by 2 seconds each time you cast Highwind Arrow."</summary>
public class RisingMoon : Talent
{
    public const int TalentId = 681;
    public const string TalentName = "Rising Moon";
    public const double ReductionSeconds = 2.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        highwind.Cast += ctx => hero.ReduceCooldown<LunarlightMark>(SimTime.FromSeconds(ReductionSeconds), ctx.CurrentTime);
    }
}
