using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 203: "Your Starfall Volley no longer consumes Lunarlight Mark stacks when triggering Lunarlight Salvo.
/// Additionally, your Lunarlight Salvo has increased critical strike chance."
/// </summary>
public partial class LunarlightAffinity : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion elarion) return;
        elarion.Salvo.KeepsMarkFor.Add(StarfallVolley.AbilityId);
        elarion.Salvo.CritChanceBonus += IncreasedInstantMarkTriggerChanceAddedCritChance;
    }
}
