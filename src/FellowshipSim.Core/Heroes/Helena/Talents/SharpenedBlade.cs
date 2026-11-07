using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 215: "Enemies afflicted by your Sweeping Strike's bleed have 20% reduced attack speed and your Power Strike deals
/// 200% more damage to them." Effect 1276 talent SweepingStrikeApplied IncreasedDamage.DamageMultiplier 3. UNVERIFIED: no log
/// has the talent.
/// </summary>
public class SharpenedBlade : Talent
{
    public const int TalentId = 215;
    public const string TalentName = "Sharpened Blade";
    public const double PowerStrikeDamage = 3.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<PowerStrike>() is not { } powerStrike || hero.GetAbility<SweepingStrikeBleed>() is not { } bleed) return;
        powerStrike.TargetMultipliers.Add((ctx, target) => bleed.IsOn(target, ctx) ? PowerStrikeDamage : 1.0);
    }
}
