namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// Dungeon affix 16: "Bosses deal 5% more damage and have 10% more health. BONUS Heroes' ability cooldowns are reduced by
/// 10%." The cooldown reduction adds to the hero's other cooldown reduction. Logs: in 12 replays of 6 Ultimatum fights
/// (4Ln6PQZCRzc7Wpta 72, c6QH41C2dYq7P3zb 13, Cf7npHNzRmALFJQy 1, GXFz3pZ94aQr2chV 3, J8NxpFaC7Vjq2mZc 7,
/// pWmcV42K39GHTLQX 23), the replay's cooldown issues fall with the reduction added, and fall less when it multiplies with
/// the hero's. A replay takes the enemies' health from the log, so the boss parts change nothing there.
/// </summary>
public sealed partial class Ultimatum : DungeonAffix
{
    /// <summary>Game data: modifier <c>CooldownReduction</c> (Additive) of effect 26, tuning <c>CooldownReductionIncrease</c>.</summary>
    public const double CooldownReduction = 0.10;

    public override int Id => AffixId;
    public override string Name => AffixName;

    public override void ApplyToHero(Hero hero) => hero.Stats.CooldownReductionPct += CooldownReduction;

    internal override void Begin(AffixRun run)
    {
        double before = run.Hero.Stats.CooldownReductionPct;
        ApplyToHero(run.Hero);
        double during = run.Hero.Stats.CooldownReductionPct;
        run.Ended += _ => run.Hero.Stats.CooldownReductionPct = before + (run.Hero.Stats.CooldownReductionPct - during);
    }
}
