using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 87: "Your Thunder Call generates 30 Fury over its duration and grants you +5% Haste while it is active."
/// Effect 1745 (21s, every 0.1s: 0.143% of maximum Fury) and effect 2753 (+5% Haste). Combat logs show neither as a buff,
/// so both are internal here. Combat logs (fXyzwGcQkTrLD34R fight 4): the Fury ticks are hasted, about 1.85 Fury a second at
/// 29% haste. Raging Tempest's Enhanced Thunder Call has them too (YC861NTRgLbmw4Qa fight 31 @ 199.7: the log gains about
/// 2.2 Fury a second more than the engine without them, with Thunder Call gone). UNVERIFIED: its haste during Raging
/// Tempest.
/// </summary>
public class RideTheLightning : Talent
{
    public const int TalentId = 87;
    public const string TalentName = "Ride the Lightning";
    public const double FuryPerTick = 0.143;
    public const double TickSeconds = 0.1;
    public const double Haste = 0.05;

    /// <summary>Effect 2753, while Thunder Call or Raging Tempest is active.</summary>
    public static readonly AuraDef HasteBuff = AuraDef.Internal("Ride the Lightning", ThunderCall.BuffSeconds,
        modifiers: StatModifier.Add(StatType.Haste, Haste));

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
                ThunderCall => ThunderCall.BuffSeconds,
                RagingTempest => RagingTempest.BuffSeconds,
                _ => 0.0,
            };
            if (seconds <= 0 || hero.SimContext is not { } ctx) return;
            // Raging Tempest takes over from the Thunder Call it removes.
            ticks?.Stop(ctx);
            hero.Auras.Apply(HasteBuff.WithDuration(seconds), now);
            ticks = PeriodicEffect.BeginHasted(ctx, hero, TalentName, seconds, TickSeconds, tickOnStart: false, partialFinalTick: false,
                onTick: (c, _) =>
                {
                    if (tariq.IsCharged(c.CurrentTime)) tariq.GainFury(FuryPerTick, c);
                });
        };
    }
}
