using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 87: "Your Thunder Call generates 30 Fury over its duration and grants you +5% Haste while it is active."
/// Effect 1745 (21s, every 0.1s: 0.143% of maximum Fury) and effect 2753 (+5% Haste). Combat logs show neither as a buff,
/// so both are internal here. Combat logs (fXyzwGcQkTrLD34R fight 4): the Fury ticks are hasted, about 1.85 Fury a second at
/// 29% haste.
/// </summary>
public class RideTheLightning : Talent
{
    public const int TalentId = 87;
    public const string TalentName = "Ride the Lightning";
    public const double FuryPerTick = 0.143;
    public const double TickSeconds = 0.1;
    public const double Haste = 0.05;

    /// <summary>Effect 2753, while Thunder Call is active.</summary>
    public static readonly AuraDef HasteBuff = AuraDef.Internal("Ride the Lightning", ThunderCall.BuffSeconds,
        modifiers: StatModifier.Add(StatType.Haste, Haste));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Tariq tariq) return;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not ThunderCall || hero.SimContext is not { } ctx) return;
            hero.Auras.Apply(HasteBuff, now);
            PeriodicEffect.BeginHasted(ctx, hero, TalentName, ThunderCall.BuffSeconds, TickSeconds, tickOnStart: false, partialFinalTick: false,
                onTick: (c, _) =>
                {
                    if (hero.Auras.HasAura(ThunderCall.Buff, c.CurrentTime)) tariq.GainFury(FuryPerTick, c);
                });
        };
        // Raging Tempest takes Thunder Call away, and its haste with it.
        hero.Auras.OnRemoved += (aura, now) =>
        {
            if (aura.Def == ThunderCall.Buff) hero.Auras.Remove(HasteBuff, now);
        };
    }
}
