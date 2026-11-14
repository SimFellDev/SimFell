using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 658: Glacial Blast becomes Talon Strike and Ice Comet becomes Rising Talons.</summary>
public partial class IcyTalons : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Rime)hero).HasIcyTalons = true;
}
