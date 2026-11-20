using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 675: "Each time your Skull Crusher and Hammer Storm deals damage to an enemy you have a Chance (PPM) to
/// cast a free Chain Lightning at them. Your Chain Lightning now has charges." Combat logs (q3y7pWgXrjN2dftw fight
/// 12): the free chains match the PPM expectation, each starting a fixed delay after the hit on that hit's enemy. UNVERIFIED: the
/// free Chain Lightning is taken to need no Thunder Call.
/// </summary>
public partial class AceOfSpades : Talent
{
    public const string ProcKey = "ace_of_spades";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is not { } chainLightning) return;
        chainLightning.SetCharges(Constants.ChargedMaxCharges);
        var ppm = new RealPpm(ProcKey, Constants.PPMChance);
        Ability?[] spenders = [hero.GetAbility<SkullCrusher>(), hero.GetAbility<HammerStorm>()];
        foreach (var spender in spenders.OfType<Ability>())
        {
            spender.Hit += (in hit) =>
            {
                if (hit.SimContext is not { } ctx || hit.Target is not { } target) return;
                if (ppm.Roll(hero, ctx, target)) chainLightning.FreeCast(ctx, target, this);
            };
        }
    }
}
