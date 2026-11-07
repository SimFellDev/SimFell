using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 429 (Elarion's Skystrider Helm, item 5899):
/// "Enemies hit by your Highwind Arrow are afflicted by Shimmer, causing you to deal more damage to them.
/// Shimmer stacks." Effect 2362 (IncomingDamageMultiplier per stack). UNVERIFIED: no
/// combat log has this legendary; the engine adds the bonus per stack.
/// </summary>
public class Shimmer : Legendary
{
    public const int PowerId = 429;
    public const int HelmItemId = 5899;
    public const int DebuffEffectId = 2362;
    public const double DebuffSeconds = 9.0;
    public const int MaxStacks = 3;
    public const double DamagePerStack = 0.08;

    public static readonly AuraDef Debuff = AuraDef.Effect("Shimmer", DebuffEffectId, DebuffSeconds, MaxStacks);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Shimmer";
    public override string HeroName => "Elarion";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Elarion || hero.GetAbility<HighwindArrow>() is not { } highwind) return;
        hero.Register(Debuff);
        highwind.Hit += (in DamageContext hit) => hit.Target?.Auras.Apply(Debuff, hit.Timestamp);
        hero.TargetDamageMultipliers.Add((target, now) => 1.0 + DamagePerStack * target.Auras.GetStacks(Debuff, now));
    }
}
