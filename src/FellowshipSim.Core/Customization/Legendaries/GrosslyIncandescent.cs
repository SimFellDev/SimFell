using FellowshipSim.Core.Heroes.Xavian;
using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 606 (Xavian's Helm of the First King, item 5899): "Each time your Brilliant Flash consumes a Swift
/// Reprieval, you gain 1 stack of Grossly Incandescent. When you reach 5 stacks of Grossly Incandescent you are granted 1
/// charge of Omega Reprieval." Effect 2723 (5 stacks). UNVERIFIED: no combat log has this legendary.
/// </summary>
public class GrosslyIncandescent : Legendary
{
    public const int PowerId = 606;
    public const int HelmItemId = 5899;
    public const int BuffEffectId = 2723;
    public const int StacksForOmega = 5;

    public static readonly AuraDef Buff = AuraDef.Effect("Grossly Incandescent", BuffEffectId, maxStacks: StacksForOmega);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Grossly Incandescent";
    public override string HeroName => "Xavian";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Xavian xavian) return;
        xavian.BrilliantFlash.SwiftReprievalConsumed += ctx =>
        {
            var now = ctx.CurrentTime;
            hero.Auras.Apply(Buff, now);
            if (hero.Auras.GetStacks(Buff, now) < StacksForOmega) return;
            hero.Auras.Remove(Buff, now);
            hero.Auras.Apply(OmegaReprieval.Buff, now);
        };
    }
}
