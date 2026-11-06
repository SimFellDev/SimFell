namespace FellowshipSim.Core.Import;

/// <summary>
/// A hero the engine does not have yet (<see cref="BuildHeroes"/>): its talent tree and main stat from game data, and
/// no abilities, so a build of it can be edited, saved and shared but not simulated. It has no constructor without
/// parameters, so the <see cref="Catalog"/> never lists it.
/// </summary>
public sealed class BuildOnlyHero(IReadOnlyList<TalentSlot> talentTree, MainStat mainStat) : Hero
{
    public override IReadOnlyList<TalentSlot> TalentTreeSlots => talentTree;

    public override MainStat MainStat => mainStat;

    public override void Initialize()
    {
    }

    public override void ResetForIteration() => ResetCombatState();
}
