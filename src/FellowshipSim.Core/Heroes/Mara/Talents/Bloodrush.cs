namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 163: "Your Hemorrhaging Strike bleed ticks faster." Game data (build 26320967): after the bleed's application,
/// `GA_Mara_BleedAttackSpender` multiplies its tick period scaler by 1 / <see cref="Constants.IncreasedTickRateWhileSingleTickRateIncrease"/>
/// (offsets 1876 to 2209) with no target count check (offsets 1658 to 1828), and ability 1517 does the same on each copy of a
/// re-application (offsets 1666 to 2197). A refresh replaces the spec, so the scaler does not compound. Combat logs
/// (fjrCax7VzGmMZw6h fight 26, Mara player 23, the one Mara player with it): after a refresh outside Maiden of Death the
/// ticks come 0.722 to 0.739 of 3s / Haste apart (@ 51.621 on enemy 77.1), with From the Shadows' 0.87.
/// </summary>
public partial class Bloodrush : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        ((Mara)hero).Hemorrhage.AbilityPeriodMultipliers.Add(1.0 / Constants.IncreasedTickRateWhileSingleTickRateIncrease);
}
