using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 74: "Your Heavy Strike has a 20% chance to cast a free Chain Lightning at the target." UNVERIFIED: no combat
/// log has this talent; the free Chain Lightning is taken to need no Thunder Call.
/// </summary>
public class BloodAndThunder : Talent
{
    public const int TalentId = 74;
    public const string TalentName = "Blood & Thunder";
    public const double ProcChance = 0.20;
    public const string ProcKey = "blood_and_thunder";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeavyStrike>() is not { } heavyStrike || hero.GetAbility<ChainLightning>() is not { } chainLightning) return;
        heavyStrike.Struck += (ctx, target) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx, target)) chainLightning.FreeCast(ctx, target, this);
        };
    }
}
