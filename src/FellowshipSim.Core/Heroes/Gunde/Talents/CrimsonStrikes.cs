using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 768 / effect 3224: "Your Blood Arc grants you Crimson Strikes, empowering your next ability that applies Rend
/// with +15% damage. Crimson Strikes does not interact with your Bloodbound Spirit ability." Combat logs
/// (Y4wpb2f3gc8LqJDX fight 25 @ 32.297-32.560): a Grim Carve spin landing after a Blood Arc uses it up, so it is used per
/// pulse of hits, not per cast; it lasts until used.
/// </summary>
public class CrimsonStrikes : Talent
{
    public const int TalentId = 768;
    public const string TalentName = "Crimson Strikes";
    public const int EffectId = 3224;
    public const double DamageMultiplier = 1.15;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId) with { Duration = SimTime.Infinite };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        gunde.GetAbility<BloodArc>()!.Arced += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
        gunde.RendPulseMultipliers.Add((ctx, ability) =>
        {
            // Blood Arc's own pulse comes before it grants the buff.
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return 1.0;
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            return DamageMultiplier;
        });
    }
}
