namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 113: "Each Combo Point you spend causes you to generate Energy" (game data),
/// so Maiden of Death multiplies it. UNVERIFIED: no combat log has this talent.
/// </summary>
public class EfficientKiller : Talent
{
    public const int TalentId = 113;
    public const string TalentName = "Efficient Killer";
    public const double EnergyPerComboPoint = 1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        hero.OnResourceSpent += (resource, amount, _) =>
        {
            if (resource == Mara.ComboPoints && amount > 0 && mara.SimContext is { } ctx) mara.GainEnergyWithMaiden(EnergyPerComboPoint * amount, ctx);
        };
    }
}
