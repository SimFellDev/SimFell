using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 422 / effect 2245: "After attacking from Stealth, you gain Assassin's Guile, causing your Queen's
/// Fang &amp; Arachnid Assault to deal more damage." Combat logs (Fc8v6yWxD14GQMnV fight 19): it comes
/// with the attack that ends Stealth.
/// </summary>
public partial class AssassinsGuile : Talent
{
    public const int EffectId = 2245;

    // Combat logs: the buff stays through the hero's death.
    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, Duration) with { KeepsOnDeath = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        mara.AttackedFromStealth += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
        // Read as the cast resolves, so the clones of Matriarch Macabre keep it.
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is QueensFang or ArachnidAssault && hero.Auras.HasAura(Buff, cast.Time)) cast.DamageMultiplier *= DamageMultiplier;
        };
    }
}
