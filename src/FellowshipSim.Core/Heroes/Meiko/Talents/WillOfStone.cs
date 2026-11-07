using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 92: "Your Shatter Earth now also activates your Spirited Strikes and Spirited Vortex buffs" (ability 1088: a vortex
/// of 105.56% of Agility every 2 seconds for 30 seconds). The vortex is taken as Spirited Vortex's own, under its damage
/// ID. UNVERIFIED: no combat log has the talent.
/// </summary>
public class WillOfStone : Talent
{
    public const int TalentId = 92;
    public const string TalentName = "Will of Stone";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ShatterEarth>() is not { } shatterEarth || hero.GetAbility<SpiritedVortex>() is not { } vortex) return;
        shatterEarth.Slammed += (ctx, target) => vortex.Activate(ctx, target);
    }
}
