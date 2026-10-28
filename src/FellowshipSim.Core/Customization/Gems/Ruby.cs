namespace FellowshipSim.Core.Customization.Gems;

/// <summary>The Ruby gem tree. Node values are from the game data (gem-ability records).</summary>
public class Ruby : Gem
{
    public static readonly GemNode[] AllNodes =
    [
        new(296, 1, "Might of the Minotaur", 80, V(("PrimaryStatIncrease", 0.02), ("HealthThreshold", 0.8)),
            AuraDef.Effect("Might of the Minotaur", 1921, modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, 1.02))),
        new(278, 2, "Champion's Heart", 150, V(("StaminaBonus", 6), ("PrimaryStatBonus", 2))),
        new(310, 3, "Unyielding Vitality", 220, V(("HealPercent", 0.007), ("Seconds", 2))),
        new(300, 4, "Titan's Blood", 300, V(("StaminaIncrease", 0.03))),
        new(330, 5, "Blessing of the Conqueror", 450, V(("DamageIncrease", 0.05)),
            AuraDef.Effect("Essence of the Conqueror", 1951, modifiers: StatModifier.More(StatType.DamageScale, 1.05))),
        new(297, 6, "Might of the Minotaur - II", 600, V(("PrimaryStatIncrease", 0.06), ("HealthThreshold", 0.8)),
            AuraDef.Effect("Might of the Minotaur - II", 1922, modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, 1.06))),
        new(279, 7, "Champion's Heart - II", 800, V(("StaminaBonus", 18), ("PrimaryStatBonus", 6))),
        new(311, 8, "Unyielding Vitality - II", 1000, V(("HealPercent", 0.021), ("Seconds", 2))),
        new(301, 9, "Titan's Blood - II", 1250, V(("StaminaIncrease", 0.09))),
        new(331, 10, "Blessing of the Conqueror - II", 1500, V(("DamageIncrease", 0.15)),
            AuraDef.Effect("Essence of the Conqueror - II", 1952, modifiers: StatModifier.More(StatType.DamageScale, 1.15))),
    ];

    /// <summary>The auras of the situational nodes at both ranks: Might of the Minotaur and Blessing of the Conqueror.</summary>
    public static readonly IReadOnlyList<AuraDef> SituationalNodeAuras =
        AllNodes.Where(n => n.Slot is 1 or 5).Select(n => n.Grants!).ToArray();

    public override GemColor Color => GemColor.Ruby;
    protected override IReadOnlyList<GemNode> NodeTable => AllNodes;

    protected override bool BindNode(Hero hero, GemNode node)
    {
        switch (node.Slot)
        {
            case 1: // Might of the Minotaur: Main Stat while above 80% health (situational: the engine does not track your health).
                // A logged Main Stat already includes it. Combat logs (Rime's combatantinfo in the 15 fights of
                // docs/accuracy/rime-accuracy-v1.md): Intellect over (hero baseAttributes Intellect 100 + gear Intellect +
                // Champion's Heart + Stoic's Teachings) is 1.100 without Ruby (GWChJ8P7tn6fHgV3 #20, #22: Mote of
                // Creation, effect 3085), 1.122-1.126 with node 296 (4Ln6PQZCRzc7Wpta #77, D1fBhVPkwYKXWbpr #1,
                // N18KtgP3rvpYGm24 #27, XNpG3AYWM8rq2zwx #19) and 1.165 with node 297 (NhBnFXmy3bJg9Cav #32, #33); the
                // aura's first logged event is its removal in every such fight, so it was up as the fight began.
                if (hero.Stats.UsesSheetValues) hero.Stats.PrimaryStat /= 1.0 + node.Value("PrimaryStatIncrease");
                hero.SituationalAuras.Add(node.Grants!);
                return true;
            case 5: // Blessing of the Conqueror: damage while in combat with a boss (situational; a sim's fight may have none).
                hero.SituationalAuras.Add(node.Grants!);
                hero.BossAuras.Add(node.Grants!);
                return true;
            case 2: // Champion's Heart: flat Main Stat (a logged Main Stat already includes it).
                if (!hero.Stats.UsesSheetValues) hero.Stats.PrimaryStat += node.Value("PrimaryStatBonus");
                return true;
            default: // Unyielding Vitality (healing) and Titan's Blood (Stamina) do not affect damage.
                return true;
        }
    }

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.PrimaryAttribute, node.Value("PrimaryStatBonus"))],
        _ => [],
    };
}
