namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 113: "Each Combo Point you spend causes you to generate Energy" (game data),
/// so Maiden of Death multiplies it. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class EfficientKiller : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        hero.OnResourceSpent += (resource, amount, _) =>
        {
            if (resource == Mara.ComboPoints && amount > 0 && mara.SimContext is { } ctx) mara.GainEnergyWithMaiden(Constants.EnergyGainPerComboPoint * amount, ctx);
        };
    }
}
