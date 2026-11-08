namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 677: each Dungeon starts with Burning Embers. Combat logs (yMAZVJL9hTpFjqbC fight 7): the first resource
/// value is the Embers' Cinders, at the first cast.
/// </summary>
public partial class BurningInitiative : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var cinders = hero.Resources.Get(Ardeos.Cinders);
        cinders.Initial += AdditionalOrbResources * Ardeos.CindersPerBurningEmber;
        cinders.Current = cinders.Initial;
    }
}
