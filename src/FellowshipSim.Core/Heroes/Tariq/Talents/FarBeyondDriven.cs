using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 79: "Each time you use Heavy Strike you gain 1 stack of Far Beyond Driven for 20 seconds, increasing your
/// Spirit by +1.5%. Far Beyond Driven stacks up to 5 times." Effect 1744.
/// </summary>
public class FarBeyondDriven : Talent
{
    public const int TalentId = 79;
    public const string TalentName = "Far Beyond Driven";
    public const int EffectId = 1744;
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 5;
    public const double SpiritPerStack = 0.015;

    public static readonly AuraDef Buff = AuraDef.Effect("Far Beyond Driven", EffectId, BuffSeconds, MaxStacks,
        StatModifier.Add(StatType.Spirit, SpiritPerStack));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeavyStrike>() is { } heavyStrike) heavyStrike.Struck += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
