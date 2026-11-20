using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 86: "Your Chain Lightning generates more Fury and each time your Chain Lightning bounces you gain a
/// stack of Thundering Vortex. Once you have enough stacks your next Chain Lightning deals more damage." Effect 2754 (its
/// own text differs from the power's in stack count and bonus). UNVERIFIED: no combat log has this legendary.
/// </summary>
public partial class ThunderingVortex : Legendary
{
    public const int BuffEffectId = 2754;
    public const int BuffMaxStacks = 40;
    public const int StacksConsumed = 20;
    public const double DamageMultiplier = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Thundering Vortex", BuffEffectId, maxStacks: BuffMaxStacks);

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is not { } chainLightning) return;
        chainLightning.FuryMultiplier *= Constants.BouncyProjectileTalentBouncyProjectileIncreasedFuryGenerationFuryScaler;
        chainLightning.Bounced += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != chainLightning || hero.Auras.GetStacks(Buff, cast.Time) < StacksConsumed) return;
            hero.Auras.ConsumeStack(Buff, cast.Time, StacksConsumed);
            cast.DamageMultiplier *= DamageMultiplier;
        };
    }
}
