using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 103: "Each cast of Shatter Earth reduces the cooldown of Twin Souls: Bulwark by 20%"
/// (Meiko.MoveToLocationAoeDamage.Talent.CooldownReduction.TauntTotem.CooldownReductionInPercentage 0.2): 20% of its 180
/// second cooldown, 36 seconds. UNVERIFIED: whether the 20% is of the full cooldown or of the time left.
/// </summary>
public class Earthbourne : Talent
{
    public const int TalentId = 103;
    public const string TalentName = "Earthbourne";
    public const double CooldownReduction = 0.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ShatterEarth>() is not { } shatterEarth || hero.GetAbility<TwinSoulsBulwark>() is not { } bulwark) return;
        shatterEarth.Slammed += (ctx, _) =>
            hero.ReduceCooldown(bulwark.Name, SimTime.FromSeconds(bulwark.Cooldown.TotalSeconds * CooldownReduction), ctx.CurrentTime);
    }
}
