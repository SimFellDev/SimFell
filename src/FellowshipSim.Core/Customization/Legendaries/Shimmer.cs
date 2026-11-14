using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 429: "Enemies hit by your Highwind Arrow are afflicted by Shimmer, causing you to deal more damage to
/// them. Shimmer stacks." Effect 2362 (IncomingDamageMultiplier per stack). UNVERIFIED: no combat log has this legendary;
/// the engine adds the bonus per stack.
/// </summary>
public partial class Shimmer : Legendary
{
    public const int DebuffEffectId = 2362;

    public static readonly AuraDef Debuff = AuraDef.Effect("Shimmer", DebuffEffectId, Constants.IncreasedIncomingDamageDebuffDuration, Constants.IncreasedIncomingDamageDebuffMaxStacks);

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion || hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        hero.Register(Debuff);
        highwind.Hit += (in DamageContext hit) => hit.Target?.Auras.Apply(Debuff, hit.Timestamp);
        hero.TargetDamageMultipliers.Add((target, now) => 1.0 + (Constants.IncreasedIncomingDamageDebuffIncomingDamageMultiplier - 1.0) * target.Auras.GetStacks(Debuff, now));
    }
}
