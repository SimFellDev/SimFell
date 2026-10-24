using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 675: "Each time your Skull Crusher and Hammer Storm deals damage to an enemy you have a Chance (3.0 PPM) to
/// cast a free Chain Lightning at them. Your Chain Lightning now has 2 charges." UNVERIFIED: no combat log has this
/// talent; the free Chain Lightning is taken to need no Thunder Call.
/// </summary>
public class AceOfSpades : Talent
{
    public const int TalentId = 675;
    public const string TalentName = "Ace of Spades";
    public const double ProcsPerMinute = 3.0;
    public const int Charges = 2;
    public const string ProcKey = "ace_of_spades";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is not { } chainLightning) return;
        chainLightning.SetCharges(Charges);
        var ppm = new RealPpm(ProcKey, ProcsPerMinute);
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
