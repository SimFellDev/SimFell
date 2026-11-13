namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// Dungeon affix 25: "Periodically, players will charge up with red lightning for 8 seconds, after which a massive bolt
/// strikes all players [...] Players who survive the massive bolt are granted Storm's Fury for 20 seconds, granting +20%
/// critical strike chance."
/// <list type="bullet">
/// <item>The charge comes every <see cref="Constants.SpawnPeriodMin"/> to <see cref="Constants.SpawnPeriodMax"/> seconds
/// of the run, uniform, from the run's start and again from each charge (<see cref="ModifierTimer"/>; Blueprint
/// GA_DungeonModifier_StormShield, ability 1946: WaitForModifierTrigger at ubergraph 4410, with no pause). A charge due in
/// a boss fight waits for the boss's window. Logs: the first Storm's Fury comes 67.1 s to 78.7 s
/// into the run, and Storm's Fury follows Storm's Fury at least 59.1 s later (median 67 s), out of combat and in boss
/// fights as well (Ab1yCxVcNBHPz6t8 fight 7: 20 bolts, 7 in boss fights; CwmX2Tb8GA6hyc1v fight 8: 18 bolts, 5).</item>
/// <item>The charge gives the hero Storm Shield (<see cref="StormShieldDuration"/> s). The bolt strikes
/// <see cref="Constants.CountdownTimer"/> s later (logs: 8.01 s, median, in each of the 9 downloaded fights with the
/// affix), and gives the hero Storm's Fury: <see cref="StormsFuryCritChance"/> crit chance for
/// <see cref="StormsFuryDuration"/> s, one stack, refreshed by a new one (effect 2699, ubergraph offset 5217).</item>
/// </list>
/// </summary>
public sealed partial class StormShield : DungeonAffix
{
    public override int Id => AffixId;
    public override string Name => AffixName;

    /// <summary>Storm Shield (effect 2700) on the hero through the charge.</summary>
    public static readonly AuraDef Shield = AuraDef.Effect(AffixName, StormShieldEffectId, StormShieldDuration);

    /// <summary>Storm's Fury (effect 2699) on the hero after the bolt.</summary>
    public static readonly AuraDef StormsFury = AuraDef.Effect("Storm's Fury", StormsFuryEffectId, StormsFuryDuration,
        modifiers: StatModifier.Add(StatType.CritChance, StormsFuryCritChance));

    internal override void Begin(AffixRun run)
    {
        void Bolt(SimulationContext c) => run.Hero.Auras.Apply(StormsFury, c.CurrentTime);
        _ = new ModifierTimer(run, Constants.SpawnPeriodMin, Constants.SpawnPeriodMax, paused: false, c =>
        {
            run.Hero.Auras.Apply(Shield, c.CurrentTime);
            c.Schedule(SimTime.FromSeconds(Constants.CountdownTimer), Bolt, "Storm Shield: Lightning Strike");
        });
    }
}
