using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 443: "Your Queen's Fang and Arachnid's Assault abilities grant you 1 stack of Malevolence: Arachnid Assault or 1
/// stack of Malevolence: Queen's Fang respectively for 20 seconds. Malevolence increases the damage of the matching
/// ability by 100%. Each Malevolence buff type stacks up to 2 times." Effects 2430 (Queen's Fang) and 2431 (Arachnid
/// Assault). Combat logs (Fc8v6yWxD14GQMnV fight 19 @ 28.487, 32.006): the finisher uses up one stack of its own buff,
/// then grants the other.
/// </summary>
public class Malevolence : Talent
{
    public const int TalentId = 443;
    public const string TalentName = "Malevolence";
    public const int QueensFangEffectId = 2430;
    public const int ArachnidAssaultEffectId = 2431;
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 2;
    public const double FinisherDamage = 2.0;

    public static readonly AuraDef QueensFangBuff = AuraDef.Effect("Malevolence: Queen's Fang", QueensFangEffectId, BuffSeconds, MaxStacks);
    public static readonly AuraDef ArachnidAssaultBuff = AuraDef.Effect("Malevolence: Arachnid Assault", ArachnidAssaultEffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            var own = cast.Ability switch { QueensFang => QueensFangBuff, ArachnidAssault => ArachnidAssaultBuff, _ => null };
            if (own == null || !hero.Auras.HasAura(own, cast.Time)) return;
            hero.Auras.ConsumeStack(own, cast.Time);
            cast.DamageMultiplier *= FinisherDamage;
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is QueensFang) hero.Auras.Apply(ArachnidAssaultBuff, now);
            else if (ability is ArachnidAssault) hero.Auras.Apply(QueensFangBuff, now);
        };
    }
}
