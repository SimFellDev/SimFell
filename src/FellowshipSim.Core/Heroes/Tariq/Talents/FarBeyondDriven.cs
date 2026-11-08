using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 79: "Each time you use Heavy Strike you gain a stack of Far Beyond Driven, increasing your
/// Spirit. Far Beyond Driven stacks." Effect 1744.
/// </summary>
public partial class FarBeyondDriven : Talent
{
    public const int EffectId = 1744;

    public static readonly AuraDef Buff = AuraDef.Effect("Far Beyond Driven", EffectId, Duration, MaxStacks,
        StatModifier.Add(StatType.Spirit, StatIncrease));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeavyStrike>() is { } heavyStrike) heavyStrike.Struck += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
