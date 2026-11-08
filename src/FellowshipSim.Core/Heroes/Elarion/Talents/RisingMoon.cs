using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>Talent 681: Each Highwind Arrow cast reduces the cooldown of Lunarlight Mark.</summary>
public partial class RisingMoon : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        highwind.Cast += ctx => hero.ReduceCooldown<LunarlightMark>(SimTime.FromSeconds(CooldownReductionInSeconds), ctx.CurrentTime);
    }
}
