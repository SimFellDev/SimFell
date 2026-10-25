using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 657: each Freezing Torrent tick has a 7% chance to cast a free Ice Comet on the target (a plain Ice Comet even
/// with Icy Talons, and it can trigger Avalanche, per the user). hero_data: OnTargetPulsatingAoe.FreeCast.ProcChance 0.07.
/// </summary>
public class ColdShower : Talent
{
    public const int TalentId = 657;
    public const string TalentName = "Cold Shower";
    public const double ProcChance = 0.07;
    public const string ProcKey = "cold_shower";

    /// <summary>
    /// The free comet strikes 10 server frames after the tick that cast it, whatever the haste. Combat logs: Ice Comet
    /// impacts with no Ice Comet cast in the 1.6s before land 0.330-0.338s after a Freezing Torrent tick
    /// (4Ln6PQZCRzc7Wpta fight 78, jHDC613ZzbTYR8wW fight 18, Rzg1nQNHpm2fhYtB fight 24, N18KtgP3rvpYGm24 fight 27,
    /// GWChJ8P7tn6fHgV3 fight 20; haste 22% to 64%).
    /// </summary>
    public const double ImpactDelaySeconds = 10.0 / 30.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var comet = hero.GetAbility<IceComet>();
        if (comet == null) return;
        hero.OnChannelTick += (ability, target, _, ctx) =>
        {
            if (ability is FreezingTorrent && target != null && ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx))
            {
                comet.FreeCast(ctx, target, source: this, countsAsCast: true, impactDelaySeconds: ImpactDelaySeconds);
            }
        };
    }
}
