using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 96: "Each hit with Earthfist Barrage adds 1 stack to your Serenity ability. Serenity now prevents your Stone
/// Shield from losing health for 0.1 seconds per stack of Serenity consumed"
/// (Meiko.ChanneledHeavyDamage.Talent.InstantSelfDefensiveCharge.DamageSoakBuff.InvinsibilityDurationPerStack 0.1). Effect
/// 2449 is on Meiko for that time. Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 34.928): a Serenity's Earthwell lasts 3.9s.
/// </summary>
public class Earthwell : Talent
{
    public const int TalentId = 96;
    public const string TalentName = "Earthwell";
    public const int BuffEffectId = 2449;
    public const double SecondsPerStack = 0.1;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<EarthfistBarrage>() is { } barrage)
        {
            barrage.Punched += (ctx, _) => hero.Auras.Apply(Serenity.Stacks, ctx.CurrentTime);
        }
        if (hero.GetAbility<Serenity>() is { } serenity)
        {
            serenity.Soothed += (ctx, stacks) =>
            {
                if (stacks > 0) hero.Auras.Apply(Buff.WithDuration(stacks * SecondsPerStack), ctx.CurrentTime);
            };
        }
    }
}
