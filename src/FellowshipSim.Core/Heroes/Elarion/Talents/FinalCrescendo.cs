using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 169: "Every 3 Highwind Arrow casts deals +100% increased damage and Ricochets up to 9 times." Effect 2222 ("At
/// 2 stacks, your next Highwind Arrow consumes all stacks and deals 100% increased damage and Ricochets to up to 9
/// additional targets"). Combat logs (fXyzwGcQkTrLD34R fight 4): the first and second casts add a stack, the third uses
/// both. The arrow strikes 9 enemies at most, the target and 8 ricochets (8 casts with 9 hits, none with more;
/// Bowguy constant Talent.StacksMoreDamageAndBounces.MaxBounces 9 counts the first hit).
/// </summary>
public class FinalCrescendo : Talent
{
    public const int TalentId = 169;
    public const string TalentName = "Final Crescendo";
    public const int BuffEffectId = 2222;
    public const int MaxStacks = 2;
    public const double DamageMultiplier = 2.0;
    public const int Ricochets = 8;

    public static readonly AuraDef Buff = AuraDef.Effect("Final Cresendo", BuffEffectId, maxStacks: MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != highwind) return;
            if (hero.Auras.GetStacks(Buff, cast.Time) < MaxStacks)
            {
                hero.Auras.Apply(Buff, cast.Time);
                return;
            }
            hero.Auras.Remove(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier;
            highwind.CastRicochets = Ricochets;
        };
    }
}
