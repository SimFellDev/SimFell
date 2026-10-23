using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>Talent 150: "The duration of your Engulfing Flames is increased by 3 seconds."</summary>
public class UndyingFlame : Talent
{
    public const int TalentId = 150;
    public const string TalentName = "Undying Flame";
    public const double ExtraSeconds = 3.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var dot = hero.GetAbility<EngulfingFlames>()?.Dot;
        if (dot != null) dot.Duration += ExtraSeconds;
    }
}
