using FellowshipSim.Core.Heroes.Xavian;
using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 606: "Each time your Brilliant Flash consumes a Swift Reprieval, you gain 1 stack of Grossly
/// Incandescent. When you reach 5 stacks of Grossly Incandescent you are granted 1 charge of Omega Reprieval." Effect 2723
/// (5 stacks). UNVERIFIED: no combat log has this legendary.
/// </summary>
public partial class GrosslyIncandescent : Legendary
{
    public const int BuffEffectId = 2723;

    public static readonly AuraDef Buff = AuraDef.Effect("Grossly Incandescent", BuffEffectId, maxStacks: Constants.NumberOfStacks);

    public override void Bind(Hero hero)
    {
        if (hero is not Xavian xavian) return;
        xavian.BrilliantFlash.SwiftReprievalConsumed += ctx =>
        {
            var now = ctx.CurrentTime;
            hero.Auras.Apply(Buff, now);
            if (hero.Auras.GetStacks(Buff, now) < Constants.NumberOfStacks) return;
            hero.Auras.Remove(Buff, now);
            hero.Auras.Apply(OmegaReprieval.Buff, now);
        };
    }
}
