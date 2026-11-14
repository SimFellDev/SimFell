using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 190 / effect 1392 ("DamageIncreaseAndCastTimeIncrease"): Glacial Blast deals more damage and has a longer cast.
/// Talon Strike benefits from talents that affect Glacial Blast (talent 658).
/// </summary>
public partial class GreaterGlacialBlast : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<GlacialBlast>() is { } glacialBlast)
        {
            glacialBlast.AddDamageMultiplier(Constants.DamageIncreaseAndCastTimeIncreaseAdditionalDamage);
            glacialBlast.ExtraCastTimeSeconds += Constants.DamageIncreaseAndCastTimeIncreaseCastTimeIncreaseDuration;
        }
        hero.GetAbility<TalonStrike>()?.AddDamageMultiplier(Constants.DamageIncreaseAndCastTimeIncreaseAdditionalDamage);
    }
}
