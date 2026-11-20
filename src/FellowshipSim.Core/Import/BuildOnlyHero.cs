namespace FellowshipSim.Core.Import;

/// <summary>
/// A hero the engine does not have yet (<see cref="BuildHeroes"/>): its talent tree, main stat and weapons from game data,
/// and no abilities, so a build of it can be edited, saved and shared but not simulated. It has no constructor without
/// parameters, so the <see cref="Catalog"/> never lists it.
/// </summary>
public sealed class BuildOnlyHero(IReadOnlyList<TalentSlot> talentTree, MainStat mainStat, IReadOnlyDictionary<int, string> weaponNames) : Hero
{
    public override IReadOnlyList<TalentSlot> TalentTreeSlots => talentTree;

    public override MainStat MainStat => mainStat;

    public override IReadOnlyDictionary<int, string> WeaponNames => weaponNames;

    public override void Initialize()
    {
    }

    public override void ResetForIteration() => ResetCombatState();
}
