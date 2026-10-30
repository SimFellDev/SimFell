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
                // Combat logs (Rime's combatantinfo in the 15 fights of docs/accuracy/rime-accuracy-v1.md): Intellect over (hero baseAttributes Intellect 100 + gear Intellect +
                // Champion's Heart + Stoic's Teachings) is 1.100 without Ruby (GWChJ8P7tn6fHgV3 #20, #22: Mote of
                // Creation, effect 3085), 1.122-1.126 with node 296 (4Ln6PQZCRzc7Wpta #77, D1fBhVPkwYKXWbpr #1,
                // N18KtgP3rvpYGm24 #27, XNpG3AYWM8rq2zwx #19) and 1.165 with node 297 (NhBnFXmy3bJg9Cav #32, #33); the
                // aura's first logged event is its removal in every such fight, so it was up as the fight began.
                hero.SituationalAuras.Add(node.Grants!);
                return true;
            case 5: // Blessing of the Conqueror: damage while in combat with a boss (situational; a sim's fight may have none).
                hero.SituationalAuras.Add(node.Grants!);
                hero.BossAuras.Add(node.Grants!);
                return true;
            case 2: // Champion's Heart: flat Main Stat and Stamina.
                hero.Stats.PrimaryStat += node.Value("PrimaryStatBonus");
                hero.Stats.Stamina += node.Value("StaminaBonus");
                return true;
            case 4: // Titan's Blood: Stamina.
                hero.Stats.StaminaMultiplier *= 1.0 + node.Value("StaminaIncrease");
                return true;
            default: // Unyielding Vitality (healing) does not affect damage.
                return true;
        }
    }

    /// <summary>
    /// The situational auras follow the active nodes of slots 1 and 5 (a rank, or a node reached or lost in a fight, or back to the bound ones).
    /// In a fight a node's aura that was up passes to the slot's new rank, and one that was off stays off (<see cref="Gem.ChangePower"/>). Here a slot
    /// that had no node and now has its first (rank 0 to rank 1) gets its aura if it is Might of the Minotaur (always: health is not modeled).
    /// Blessing of the Conqueror stays off: the engine has no boss in combat to ask (TODO: turn it on against a boss once it does). A replay's log
    /// gives the same auras their events.
    /// </summary>
    protected override void NodesChanged(Hero hero, IReadOnlyList<GemNode> before, SimTime? now)
    {
        foreach (int slot in new[] { 1, 5 })
        {
            var old = before.FirstOrDefault(n => n.Slot == slot)?.Grants;
            var next = NodeInSlot(slot)?.Grants;
            if (old == next) continue;
            if (old != null)
            {
                hero.SituationalAuras.Remove(old);
                if (slot == 5) hero.BossAuras.Remove(old);
            }
            if (next != null)
            {
                hero.SituationalAuras.Add(next);
                if (slot == 5) hero.BossAuras.Add(next);
            }
            if (now is not { } time || old != null || next == null) continue;
            // TODO: Might of the Minotaur is up above 80% health. The engine does not model the hero's health, so it is taken as high; once
            // it is, check the health here (and when it crosses 80%) instead of turning the aura on.
            // TODO: Blessing of the Conqueror (slot 5) is up while an enemy in combat is a boss. It stays off here until the engine can tell a boss
            // (the boss flag of an enemy): then turn it on at rank 0 to rank 1 when one is in combat.
            if (slot == 1) hero.Auras.Apply(hero.Register(next), time);
        }
    }

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.PrimaryAttribute, node.Value("PrimaryStatBonus"))],
        _ => [],
    };
}
