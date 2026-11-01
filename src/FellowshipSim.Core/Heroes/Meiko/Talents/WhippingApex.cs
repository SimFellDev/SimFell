using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 626: "When your Lashing Stormkick consumes a Stormfall, it adds 1 stack to your Serenity ability per target hit,
/// up to a maximum of 5 stacks per Lashing Stormkick" (Meiko.HighDamageCleave.Talent.InstantSelfDefensive.MaxStacks 5).
/// </summary>
public class WhippingApex : Talent
{
    public const int TalentId = 626;
    public const string TalentName = "Whipping Apex";
    public const int MaxStacksPerKick = 5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<LashingStormkick>() is not { } lashing) return;
        lashing.Kicked += (ctx, _, _, struck, stormfall) =>
        {
            int stacks = Math.Min(struck, MaxStacksPerKick);
            if (stormfall && stacks > 0) hero.Auras.Apply(Serenity.Stacks, ctx.CurrentTime, stacks);
        };
    }
}
