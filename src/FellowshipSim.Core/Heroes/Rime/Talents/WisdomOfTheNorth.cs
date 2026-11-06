using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 49: Ice Blitz, Flight of the Navir and Winter's Blessing cooldowns are reduced per Winter Orb spent or overflowed.</summary>
public class WisdomOfTheNorth : Talent
{
    public const int TalentId = 49;
    public const string TalentName = "Wisdom of the North";
    public const double ReductionPerOrbSeconds = 0.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        Ability?[] reduced = [hero.GetAbility<IceBlitz>(), hero.GetAbility<FlightOfTheNavir>(), hero.GetAbility<WintersBlessing>()];
        void ReduceCooldowns(int orbs, SimTime now)
        {
            var amount = SimTime.FromSeconds(ReductionPerOrbSeconds * orbs);
            foreach (var ability in reduced)
            {
                if (ability != null) hero.ReduceCooldown(ability.Name, amount, now);
            }
        }
        // "...for each Winters Orb you spend or that overflows your maximum."
        hero.OnResourceSpent += (resource, amount, now) =>
        {
            if (resource == Rime.WinterOrbsResource) ReduceCooldowns((int)amount, now);
        };
        hero.OnResourceOverflow += (resource, amount, now) =>
        {
            if (resource == Rime.WinterOrbsResource) ReduceCooldowns((int)amount, now);
        };
    }
}
