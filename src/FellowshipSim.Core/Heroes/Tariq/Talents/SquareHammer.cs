using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 673: "Your Heavy Strike grants you a stack of Square Hammer, stacking. Your next
/// Skull Crusher or Hammer Storm consumes all stacks, reducing the cooldown of your Thunder Call per stack.
/// Each time you consume Square Hammer you are granted Expertise." Effects 3158 (the stacks) and 3159
/// (the Expertise).
/// </summary>
public class SquareHammer : Talent
{
    public const int TalentId = 673;
    public const string TalentName = "Square Hammer";
    public const int StackEffectId = 3158;
    public const double StackSeconds = 20.0;
    public const int MaxStacks = 5;
    public const double CooldownPerStackSeconds = 1.0;
    public const int ActiveEffectId = 3159;
    public const double ActiveSeconds = 4.0;
    public const double Expertise = 0.08;

    public static readonly AuraDef Stacks = AuraDef.Effect("Square Hammer", StackEffectId, StackSeconds, MaxStacks);

    public static readonly AuraDef Active = AuraDef.Effect("Square Hammer: Active", ActiveEffectId, ActiveSeconds,
        modifiers: StatModifier.Add(StatType.Expertise, Expertise));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeavyStrike>() is { } heavyStrike) heavyStrike.Struck += (ctx, _) => hero.Auras.Apply(Stacks, ctx.CurrentTime);
        // Consumed as the spender resolves, so its own hits have the Expertise.
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not (SkullCrusher or HammerStorm)) return;
            int stacks = hero.Auras.GetStacks(Stacks, cast.Time);
            if (stacks == 0) return;
            hero.Auras.Remove(Stacks, cast.Time);
            hero.ReduceCooldown<ThunderCall>(SimTime.FromSeconds(CooldownPerStackSeconds * stacks), cast.Time);
            hero.Auras.Apply(Active, cast.Time);
        };
    }
}
