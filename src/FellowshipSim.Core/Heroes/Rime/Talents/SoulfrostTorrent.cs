using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 195 / effect 1373: casting an ability can grant Soulfrost Torrent (a PPM proc, not while it is up); the next
/// Freezing Torrent ticks faster and has added crit chance. Combat logs (Rime's 15 fights of patch 0.5.0.0): Freezing
/// Torrent crits match the engine with both crit terms. UNVERIFIED: the procs per minute.
/// </summary>
public partial class SoulfrostTorrent : Talent
{
    public const int EffectId = 1373;

    /// <summary>
    /// The empowered tick's damage also scales with 1 + the hero's CritChance attribute (game data: damage scale x (1 + CritChance), the owner's attribute read at the
    /// tick, with neither the +1.0 nor an ability's or a target's own crit bonus). This models the game's current
    /// behaviour, a developer bug, until the game fixes it; no other damage has the term.
    /// </summary>
    public const double DamageScaleBase = 1.0;

    /// <summary>The empowered tick's damage scale (<see cref="DamageScaleBase"/> + the hero's CritChance attribute).</summary>
    public static double TickDamageScale(Hero hero, SimTime now) => DamageScaleBase + hero.CritChance(now);
    public const string ProcKey = "soulfrost_torrent";

    public static readonly AuraDef Buff = AuraDef.Effect("Soulfrost Torrent", EffectId, BuffDuration) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var rime = (Rime)hero;
        var rppm = new RealPpm(ProcKey, Ppm);
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
