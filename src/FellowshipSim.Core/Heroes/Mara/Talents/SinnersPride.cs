using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>Talent 586: "Each Combo Point you spend reduces the cooldown of your Maiden of Death ability by 0.6 seconds."</summary>
public class SinnersPride : Talent
{
    public const int TalentId = 586;
    public const string TalentName = "Sinner's Pride";
    public const double SecondsPerComboPoint = 0.6;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnResourceSpent += (resource, amount, now) =>
        {
            if (resource == Mara.ComboPoints && amount > 0) hero.ReduceCooldown<MaidenOfDeath>(SimTime.FromSeconds(SecondsPerComboPoint * amount), now);
        };
    }
}
