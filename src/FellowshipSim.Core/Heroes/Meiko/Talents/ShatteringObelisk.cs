namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 415: "The Sibling of Earth created by your Twin Souls: Bulwark ability has 50% increased health and duration. The
/// Sibling of Earth accumulates 15% of all damage it takes and pulses it out to nearby enemies up to 3 times, when reaching
/// 65% Health, 30% Health, and finally upon being destroyed. If Sibling of Earth expires due to its duration, it deals 15% of
/// any remaining health it has. ... Damage is reduced beyond 1 targets." The pulse is the Sibling of Earth's ability 1551,
/// effect 2261 (Magic.Wind). A pulse comes from damage the Sibling of Earth takes, so it is a defensive proc. It fires in a
/// replay only, at the log's pulses. Its amount is 15% of that damage, so each pulse hit has the log's
/// amount (Hero.DealDefensiveProcHit), with no crit roll.
/// UNVERIFIED: no combat log has the talent, so the pulse's log ID (effect 2261) is unconfirmed.
/// </summary>
public class ShatteringObelisk : Talent
{
    public const int TalentId = 415;
    public const string TalentName = "Shattering Obelisk";
    public const string ProcKey = "shattering_obelisk";
    public const int PulseEffectId = 2261;

    /// <summary>The pulse's damage ID in the combat logs. UNVERIFIED.</summary>
    public static int PulseLogId => LogIds.Effect(PulseEffectId);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.AddDefensiveProc(ProcKey, (ctx, _) =>
    {
        foreach (var enemy in ctx.Outcomes.SelectTargets(PulseLogId, ctx.ActiveEnemies, ctx))
        {
            hero.DealDefensiveProcHit(ctx, enemy, PulseLogId, TalentName);
        }
    });
}
