namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 4: "Every 20 Critical Strikes you gain 21 / 39 / 57 / 75 Critical Strike rating for 12 seconds. You can not
/// gain stacks while Seized Opportunity is active on you." (counter: effect 2111, buff: effect 2110)
/// </summary>
public class SeizedOpportunity : Trait
{
    public const int TraitId = 4;
    public const string TraitName = "Seized Opportunity";
    public const int CounterEffectId = 2111;
    public const int CritsNeeded = 20;
    public const int BuffEffectId = 2110;
    public const double DurationSeconds = 12.0;
    public static readonly double[] CritRatingByRank = [21, 39, 57, 75];

    public static readonly AuraDef Counter = AuraDef.Effect("Seized Opportunity", CounterEffectId, maxStacks: CritsNeeded);

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        var buff = hero.Register(AuraDef.Effect("Seized Opportunity", BuffEffectId, DurationSeconds,
            modifiers: StatModifier.Add(StatType.CritRating, ForRank(CritRatingByRank))));
        hero.OnDamageDealt += (in dmg) =>
        {
            if (!dmg.IsCrit || hero.Auras.HasAura(buff, dmg.Timestamp)) return;
            var stacks = hero.Auras.Apply(Counter, dmg.Timestamp).Stacks;
            if (stacks >= Counter.MaxStacks)
            {
                hero.Auras.Remove(Counter, dmg.Timestamp);
                hero.Auras.Apply(buff, dmg.Timestamp);
            }
        };
    }
}
