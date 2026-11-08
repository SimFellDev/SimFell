namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 4: "Every set number of Critical Strikes you gain Critical Strike rating for a time. You can not
/// gain stacks while Seized Opportunity is active on you." (counter: effect 2111, buff: effect 2110)
/// </summary>
public partial class SeizedOpportunity : Sigil
{
    public const int CounterEffectId = 2111;
    public const int CritsNeeded = 20;
    public const int BuffEffectId = 2110;
    public const double DurationSeconds = 12.0;

    // Combat logs: the counter and the buff stay through the hero's death (18 deaths, none removed it).
    public static readonly AuraDef Counter = AuraDef.Effect("Seized Opportunity", CounterEffectId, maxStacks: CritsNeeded) with { KeepsOnDeath = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var buff = hero.Register(AuraDef.Effect("Seized Opportunity", BuffEffectId, DurationSeconds,
            modifiers: StatModifier.Add(StatType.CritRating, ForRank(BuffAdditionalCritRatingByRank))) with { KeepsOnDeath = true });
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
