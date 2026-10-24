namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 677: "Start each Dungeon with 2 Burning Embers" (200 Cinders). Combat logs (yMAZVJL9hTpFjqbC fight 7): the
/// first reading is 200 Cinders, at the first cast.
/// </summary>
public class BurningInitiative : Talent
{
    public const int TalentId = 677;
    public const string TalentName = "Burning Initiative";
    public const int BurningEmbers = 2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var cinders = hero.Resources.Get(Ardeos.Cinders);
        cinders.Initial += BurningEmbers * Ardeos.CindersPerBurningEmber;
        cinders.Current = cinders.Initial;
    }
}
