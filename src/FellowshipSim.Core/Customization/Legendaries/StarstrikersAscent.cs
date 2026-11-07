using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 430 (Elarion's Skystrider Hauberk, item 5898):
/// "Your Spirit Refunds have a chance to reset the cooldown of your Heartseeker Barrage and grant you
/// Impending Heartseeker. Impending Heartseeker causes your next Heartseeker Barrage to deal increased
/// damage with each arrow during its channel." Effect 2317.
/// </summary>
public class StarstrikersAscent : Legendary
{
    public const int PowerId = 430;
    public const int HauberkItemId = 5898;
    public const double ProcChance = 0.5;
    public const string ProcKey = "starstrikers_ascent";

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Starstriker's Ascent";
    public override string HeroName => "Elarion";
    public override IReadOnlyList<int> ItemIds => [HauberkItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion) return;
        hero.OnSpiritRefund += (_, _, ctx) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) ImpendingHeartseeker.Grant(hero, ctx);
        };
    }
}
