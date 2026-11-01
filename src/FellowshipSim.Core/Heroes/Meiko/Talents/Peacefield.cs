using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 174: "Your Serenity applies Peacefield on you, healing you for 80% of the initial heal's value over 9 seconds"
/// (Meiko.InstantSelfDefensive.Talent.ApplyHot.Hot Duration 9, PercentageOfTotalHeal 0.8, TickInterval 1.5). Effect 2256
/// is on Meiko for its 9 seconds. Healing is not simulated.
/// </summary>
public class Peacefield : Talent
{
    public const int TalentId = 174;
    public const string TalentName = "Peacefield";
    public const int BuffEffectId = 2256;
    public const double BuffSeconds = 9.0;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<Serenity>() is not { } serenity) return;
        serenity.Soothed += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
