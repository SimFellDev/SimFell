namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 571: "Your Shining Halo deals 25% increased damage and its duration is increased by 6 seconds." (ability 1859
/// constant Talent.DamageIncrease.Multiplier 1.25). UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class GrandHalo : Talent
{
    public const double BonusSeconds = 6.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var halo = ((Xavian)hero).ShiningHalo;
        halo.AddDamageMultiplier(Constants.DamageIncreaseMultiplier);
        halo.BonusDurationSeconds += BonusSeconds;
    }
}
