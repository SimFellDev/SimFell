using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 657: each Freezing Torrent tick has a chance to cast a free Ice Comet on the target (a plain Ice Comet even
/// with Icy Talons, and it can trigger Avalanche, per the user)./// </summary>
public partial class ColdShower : Talent
{
    public const string ProcKey = "cold_shower";

    /// <summary>
    /// The free comet strikes a fixed number of server frames after the tick that cast it, whatever the haste. Combat
    /// logs: Ice Comet impacts with no recent Ice Comet cast land that long after a Freezing Torrent tick
    /// (4Ln6PQZCRzc7Wpta fight 78, jHDC613ZzbTYR8wW fight 18, Rzg1nQNHpm2fhYtB fight 24, N18KtgP3rvpYGm24 fight 27,
    /// GWChJ8P7tn6fHgV3 fight 20; across a range of haste).
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
            if (ability is FreezingTorrent && target != null && ctx.Outcomes.RollProc(ProcKey, Constants.ChanneledBeamSingleDamageTalentOnTargetPulsatingAoeFreeCastProcChance, ctx))
            {
                comet.FreeCast(ctx, target, source: this, countsAsCast: true, impactDelaySeconds: ImpactDelaySeconds);
            }
        };
    }
}
