namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 108: "When your Hemorrhaging Strike bleed is applied to an enemy you gain Critical Strike chance. You gain
/// additional Critical Strike chance for every additional enemy that has your Hemorrhaging Strike bleed applied,
/// up to a maximum total." UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class RedLedger : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        hero.TargetCritChanceBonuses.Add((_, _) =>
        {
            int bleeding = mara.SimContext is { } ctx ? mara.Hemorrhage.ActiveCount(ctx) : 0;
            return bleeding == 0 ? 0.0 : Math.Min(MaxThreshold, AdditionalHaste + AdditionalHastePerAdditionalTarget * (bleeding - 1));
        });
    }
}
