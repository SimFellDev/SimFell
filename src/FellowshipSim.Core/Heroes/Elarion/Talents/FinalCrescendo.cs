using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 169: Every few Highwind Arrow casts deal increased damage and Ricochet more times. Effect 2222 (at
/// its stack threshold, the next Highwind Arrow consumes all stacks and deals increased damage and Ricochets to additional
/// targets). Combat logs (fXyzwGcQkTrLD34R fight 4): casts add stacks up to the threshold, and the next uses
/// them. The arrow strikes a capped number of enemies, the target and its ricochets (no cast had more hits;
/// the game data's maximum bounces counts the first hit).
/// </summary>
public partial class FinalCrescendo : Talent
{
    public const int BuffEffectId = 2222;

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
            highwind.CastRicochets = MaxBounces - 1;
        };
    }
}
