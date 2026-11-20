using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 443: "Your Queen's Fang and Arachnid's Assault abilities grant you a stack of Malevolence: Arachnid Assault or a
/// stack of Malevolence: Queen's Fang respectively. Malevolence increases the damage of the matching
/// ability. Each Malevolence buff type stacks." Effects 2430 (Queen's Fang) and 2431 (Arachnid
/// Assault). Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 28.487, 32.006): the finisher uses up one stack of its own buff,
/// then grants the other.
/// </summary>
public partial class Malevolence : Talent
{
    public const int QueensFangEffectId = 2430;
    public const int ArachnidAssaultEffectId = 2431;

    public static readonly AuraDef QueensFangBuff = AuraDef.Effect("Malevolence: Queen's Fang", QueensFangEffectId, Constants.TalentDamageSpendersBuffOtherSpenderDuration, Constants.TalentDamageSpendersBuffOtherSpenderMaxNumberOfStacks);
    public static readonly AuraDef ArachnidAssaultBuff = AuraDef.Effect("Malevolence: Arachnid Assault", ArachnidAssaultEffectId, Constants.TalentDamageSpendersBuffOtherSpenderDuration, Constants.TalentDamageSpendersBuffOtherSpenderMaxNumberOfStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            var own = cast.Ability switch { QueensFang => QueensFangBuff, ArachnidAssault => ArachnidAssaultBuff, _ => null };
            if (own == null || !hero.Auras.HasAura(own, cast.Time)) return;
            hero.Auras.ConsumeStack(own, cast.Time);
            cast.DamageMultiplier *= Constants.TalentDamageSpendersBuffOtherSpenderDamageScaler;
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is QueensFang) hero.Auras.Apply(ArachnidAssaultBuff, now);
            else if (ability is ArachnidAssault) hero.Auras.Apply(QueensFangBuff, now);
        };
    }
}
