using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 430: "Your Spirit Refunds have a chance to reset the cooldown of your Heartseeker Barrage and grant you
/// Impending Heartseeker. Impending Heartseeker causes your next Heartseeker Barrage to deal increased damage with each
/// arrow during its channel." Effect 2317.
/// </summary>
public partial class StarstrikersAscent : Legendary
{
    public const string ProcKey = "starstrikers_ascent";

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion) return;
        hero.OnSpiritRefund += (_, _, ctx) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, Constants.SpiritProcResetAndBuff, ctx)) ImpendingHeartseeker.Grant(hero, ctx);
        };
    }
}
