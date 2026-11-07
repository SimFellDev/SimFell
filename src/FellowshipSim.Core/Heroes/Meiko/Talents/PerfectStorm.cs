using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 91: "Your Lashing Stormkick has a 25% chance to deal damage an additional time for 100% of the initial hit. The
/// chance for Perfect Storm to trigger is increased by your Haste" (Meiko.HighDamageCleave.Talent.DoubleStrike Chance 0.25,
/// DamageMultiplier 1, DelayBetweenStrikes 0.4). Logged as ability 2310 ("Lashing Stormkick"). Combat logs (27 fights,
/// 6,158 extra hits): 0.40s after the kick, on every enemy it struck, at 1.01x the kick's hit at the median, each with a
/// crit roll of its own (35% of them crit where the kick did not, or the reverse). The chance's Haste scaling is taken as
/// 25% x (1 + Haste): UNVERIFIED.
/// </summary>
public class PerfectStorm : Talent
{
    public const int TalentId = 91;
    public const string TalentName = "Perfect Storm";
    public const string ProcKey = "perfect_storm";
    public const int DamageId = 2310;
    public const double Chance = 0.25;
    public const double DamageMultiplier = 1.0;
    public const double DelaySeconds = 0.4;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<LashingStormkick>() is not { } lashing) return;
        lashing.Kicked += (ctx, target, multiplier, _, _) =>
        {
            double chance = Chance * (1.0 + hero.GetHaste(ctx.CurrentTime));
            if (!ctx.Outcomes.RollProc(ProcKey, chance, ctx, target)) return;
            ctx.Schedule(SimTime.FromSeconds(DelaySeconds),
                c => lashing.Kick(c, target, multiplier * DamageMultiplier, DamageId, TalentName), TalentName);
        };
    }
}
