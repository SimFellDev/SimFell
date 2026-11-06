using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 86 (Tariq's Stormlord Harness, item 5898; Bloodied Brass Loop, item 5221; Loop of the Thundering
/// Vortex, item 5222): "Your Chain Lightning generates more Fury and each time your Chain Lightning bounces you gain a
/// stack of Thundering Vortex. Once you have enough stacks your next Chain Lightning deals more damage." Effect 2754 (its
/// own text differs from the power's in stack count and bonus). UNVERIFIED: no combat log has this legendary.
/// </summary>
public class ThunderingVortex : Legendary
{
    public const int PowerId = 86;
    public const int HarnessItemId = 5898;
    public const int LoopItemId = 5221;
    public const int VortexLoopItemId = 5222;
    public const int BuffEffectId = 2754;
    public const int MaxStacks = 40;
    public const int StacksConsumed = 20;
    public const double FuryMultiplier = 1.5;
    public const double DamageMultiplier = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Thundering Vortex", BuffEffectId, maxStacks: MaxStacks);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Thundering Vortex";
    public override string HeroName => "Tariq";
    public override IReadOnlyList<int> ItemIds => [HarnessItemId, LoopItemId, VortexLoopItemId];

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is not { } chainLightning) return;
        chainLightning.FuryMultiplier *= FuryMultiplier;
        chainLightning.Bounced += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != chainLightning || hero.Auras.GetStacks(Buff, cast.Time) < StacksConsumed) return;
            hero.Auras.ConsumeStack(Buff, cast.Time, StacksConsumed);
            cast.DamageMultiplier *= DamageMultiplier;
        };
    }
}
