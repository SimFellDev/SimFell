using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 398: Winter's Blessing grants stacks of Undulating Spirit (effect 2171); the next ability that
/// spends Winter Orbs consumes a stack and always triggers a Spirit Refund (handled in <see cref="Rime.SpendWinterOrbs"/>).
/// The Spirit Points it gave at the start of each dungeon were removed in a patch (game data: power 398
/// generates nothing).
/// </summary>
public class UndulatingSpirit : Legendary
{
    public const int PowerId = 398;
    public const int BuffEffectId = 2171;
    public const int StacksGranted = 2;
    /// <summary>Harbinger of Winter Furs.</summary>
    public const int ChestItemId = 5898;
    public override int LegendaryPowerId => PowerId;
    public override string Name => "Undulating Spirit";
    public override string HeroName => "Rime";
    public override IReadOnlyList<int> ItemIds => [ChestItemId];

    public static readonly AuraDef Buff = AuraDef.Effect("Undulating Spirit", BuffEffectId, maxStacks: StacksGranted);

    public override void Bind(Hero hero)
    {
        if (hero is not Rime rime) return;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is WintersBlessing) rime.Auras.Apply(Buff, now, setStacks: StacksGranted);
        };
        // "Your next ability that spends Winter Orbs consumes a stack of Undulating Spirits and always triggers a Spirit Refund."
        hero.GuaranteedSpiritRefunds.Add(now =>
        {
            if (!hero.Auras.HasAura(Buff, now)) return false;
            hero.Auras.ConsumeStack(Buff, now);
            return true;
        });
    }
}
