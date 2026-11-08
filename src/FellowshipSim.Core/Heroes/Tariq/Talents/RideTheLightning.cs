using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 87: "Your Thunder Call generates Fury over its duration and grants you Haste while it is active."
/// Effect 1745 (every <see cref="TickInterval"/>: a share of maximum Fury) and effect 2753 (a Haste bonus). Combat logs show neither as a buff,
/// so both are internal here. The Fury ticks are hasted by the haste as Thunder Call is cast, for the whole buff (confirmed
/// in game, 2026-11-01; combat logs, fXyzwGcQkTrLD34R fight 4). Raging Tempest's
/// Enhanced Thunder Call has them too (YC861NTRgLbmw4Qa fight 31 @ 199.7: the log gains more Fury a second than
/// the engine without them, with Thunder Call gone). UNVERIFIED: its haste during Raging Tempest, and whether the snapshot
/// has the talent's own Haste (here it does).
/// </summary>
public partial class RideTheLightning : Talent
{
    /// <summary>Effect 2753, while Thunder Call or Raging Tempest is active.</summary>
    public static readonly AuraDef HasteBuff = AuraDef.Internal("Ride the Lightning", ThunderCall.AbilityDuration,
        modifiers: StatModifier.Add(StatType.Haste, AddedHaste));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Tariq tariq) return;
        PeriodicEffect? ticks = null;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            double seconds = ability switch
            {
                ThunderCall => ThunderCall.AbilityDuration,
                RagingTempest => RagingTempest.BuffSeconds,
                _ => 0.0,
            };
            if (seconds <= 0 || hero.SimContext is not { } ctx) return;
            // Raging Tempest takes over from the Thunder Call it removes.
            ticks?.Stop(ctx);
            hero.Auras.Apply(HasteBuff.WithDuration(seconds), now);
            // The ticks' haste is the haste now, until the buff ends.
            double period = TickInterval / (1.0 + hero.GetHaste(now));
            ticks = PeriodicEffect.Begin(ctx, TalentName, seconds, period, tickOnStart: false, partialFinalTick: false,
                onTick: (c, _) =>
                {
                    if (tariq.IsCharged(c.CurrentTime)) tariq.GainFury(RagePerTick * Tariq.Fury.Max, c);
                });
        };
    }
}
