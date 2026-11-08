using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 768 / effect 3224: "Your Blood Arc grants you Crimson Strikes, empowering your next ability that applies Rend
/// with increased damage. Crimson Strikes does not interact with your Bloodbound Spirit ability." It lasts until used. The
/// cast that uses it gets the bonus on all its hits. Combat logs (Y4wpb2f3gc8LqJDX fight 25 @ 32.297-32.560): a spin of a Grim
/// Carve cast before the Blood Arc uses it up too, without the bonus (that fight's 330 such spins are well below the
/// engine's with the bonus, while the other spins are close to it).
/// </summary>
public partial class CrimsonStrikes : Talent
{
    public const string TalentName = "Crimson Strikes";
    public const int EffectId = 3224;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId) with { Duration = SimTime.Infinite };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        gunde.GetAbility<BloodArc>()!.Arced += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
        gunde.RendCastMultipliers.Add((ctx, _) =>
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return 1.0;
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            return DamageMultiplier;
        });
        // Blood Arc's own pulse comes before it grants the buff.
        gunde.RendPulse += (ctx, _) => hero.Auras.Remove(Buff, ctx.CurrentTime);
    }
}
