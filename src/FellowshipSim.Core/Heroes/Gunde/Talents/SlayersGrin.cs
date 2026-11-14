using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>Talent 763: "Your Rupture cooldown is reduced each time you collect a Blood Feather."</summary>
public partial class SlayersGrin : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        gunde.FeatherCollected += ctx => hero.ReduceCooldown<Rupture>(SimTime.FromSeconds(Constants.ReductionInSeconds), ctx.CurrentTime);
    }
}
