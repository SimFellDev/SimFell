using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>Talent 150: Engulfing Flames lasts longer.</summary>
public partial class UndyingFlame : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var dot = hero.GetAbility<EngulfingFlames>()?.Dot;
        if (dot != null) dot.Duration += ExtendedDurationDurationIncrease;
    }
}
