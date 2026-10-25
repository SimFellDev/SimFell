using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 195 / effect 1373 (24s): casting an ability can grant Soulfrost Torrent (1.2 PPM, not while it is up); the next
/// Freezing Torrent ticks 40% faster and has +100% crit chance. hero_data Freezing Torrent "BoostByCritProc"
/// (Ppm 1.2, ChannelTickRateIncrease 1.4, CritChance 1.0). Combat logs (Rime's 15 fights of patch 0.5.0.0): Freezing
/// Torrent crits are 1.007 of the engine with both crit terms. UNVERIFIED: the 1.2 procs per minute.
/// </summary>
public class SoulfrostTorrent : Talent
{
    public const int TalentId = 195;
    public const string TalentName = "Soulfrost Torrent";
    public const int EffectId = 1373;
    /// <summary>Datamine export 26136736: effect 1373 duration 24 and the tooltip's "24 seconds" (18 before the patch).</summary>
    public const double BuffSeconds = 24.0;
    public const double ProcsPerMinute = 1.2;
    public const double TickRateMultiplier = 1.4;
    /// <summary>
    /// Crit chance a tick of the empowered Freezing Torrent gets: effect 1391's execution modifier Additive
    /// SetByCaller.CritChance on the captured CritChance, source tag BoostByCritProc.ProcActive (effect 2307), row
    /// Rime.ChanneledBeamSingleDamage.BoostByCritProc.CritChance.
    /// </summary>
    public const double CritChance = 1.0;

    /// <summary>
    /// The empowered tick's damage also scales with 1 + the hero's CritChance attribute (GA_Rime_ChanneledBeamSingleDamage
    /// ubergraph 2513-2914, build 26162532: SetByCaller.DamageScale x (1 + CritChance), the owner's attribute read at the
    /// tick, with neither the +1.0 nor an ability's or a target's own crit bonus). This models the game's current
    /// behaviour, a developer bug, until the game fixes it; no other damage has the term.
    /// </summary>
    public const double DamageScaleBase = 1.0;

    /// <summary>The empowered tick's damage scale (<see cref="DamageScaleBase"/> + the hero's CritChance attribute).</summary>
    public static double TickDamageScale(Hero hero, SimTime now) => DamageScaleBase + hero.CritChance(now);
    public const string ProcKey = "soulfrost_torrent";

    public static readonly AuraDef Buff = AuraDef.Effect("Soulfrost Torrent", EffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var rime = (Rime)hero;
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is { } ctx && !rime.Auras.HasAura(Buff, now)
                && rppm.Roll(hero, ctx))
            {
                rime.Auras.Apply(Buff, now);
            }
        };
    }
}
