namespace FellowshipSim.Core.Customization.Gems;

/// <summary>The Topaz gem tree. Node values are from the game data (gem-ability records).</summary>
public class Topaz : Gem
{
    public const double AdrenalineRushSeconds = 10.0;

    public static readonly GemNode[] AllNodes =
    [
        new(318, 1, "Adrenaline Rush", 80, V(("HasteBonus", 0.03), ("Duration", AdrenalineRushSeconds), ("HealthThreshold", 0.3)),
            AuraDef.Effect("Adrenaline Rush", 1939, AdrenalineRushSeconds, modifiers: StatModifier.Add(StatType.Haste, 0.03))),
        new(280, 2, "Thief's Alacrity", 150, V(("StaminaBonus", 4), ("HasteBonus", 8))),
        new(344, 3, "Rogue's Resurgence", 220, V(("HealthThreshold", 0.5), ("HealPercent", 0.08), ("Cooldown", 20))),
        new(320, 4, "Feline Grace", 300, V(("HasteBonus", 0.02))),
        new(316, 5, "Blessing of the Virtuoso", 450, V(("HasteBonus", 0.02)),
            AuraDef.Effect("Essence of the Virtuoso", 1937, modifiers: StatModifier.Add(StatType.Haste, 0.02))),
        new(319, 6, "Adrenaline Rush - II", 600, V(("HasteBonus", 0.09), ("Duration", AdrenalineRushSeconds), ("HealthThreshold", 0.3)),
            AuraDef.Effect("Adrenaline Rush - II", 1940, AdrenalineRushSeconds, modifiers: StatModifier.Add(StatType.Haste, 0.09))),
        new(281, 7, "Thief's Alacrity - II", 800, V(("StaminaBonus", 12), ("HasteBonus", 24))),
        new(345, 8, "Rogue's Resurgence - II", 1000, V(("HealthThreshold", 0.5), ("HealPercent", 0.24), ("Cooldown", 20))),
        new(321, 9, "Feline Grace - II", 1250, V(("HasteBonus", 0.06))),
        new(317, 10, "Blessing of the Virtuoso - II", 1500, V(("HasteBonus", 0.06)),
            AuraDef.Effect("Essence of the Virtuoso - II", 1938, modifiers: StatModifier.Add(StatType.Haste, 0.06))),
    ];

    public override GemColor Color => GemColor.Topaz;
    protected override IReadOnlyList<GemNode> NodeTable => AllNodes;

    protected override bool BindNode(Hero hero, GemNode node)
    {
        switch (node.Slot)
        {
            case 1: // Adrenaline Rush: Haste for 10s each time you deal damage to an enemy at 30% health or less.
                BindAdrenalineRush(hero, node);
                return true;
            case 4: // Feline Grace: Haste.
                hero.Stats.BonusHaste += node.Value("HasteBonus");
                return true;
            case 2: // Thief's Alacrity: flat Stamina and Haste rating.
                hero.Stats.HasteRating += node.Value("HasteBonus");
                hero.Stats.Stamina += node.Value("StaminaBonus");
                return true;
            case 5: // Blessing of the Virtuoso: "You retain X Haste from Spirit of Heroism while it is not active."
                hero.PassiveAuras.Add(node.Grants!);
                BindVirtuoso(hero, node);
                return true;
            default: // Rogue's Resurgence (healing).
                return true;
        }
    }

    // Adrenaline Rush and Essence of the Virtuoso of the active ranks (null: none).
    private AuraDef? _rush, _essence;
    private double _rushThreshold;

    private void BindAdrenalineRush(Hero hero, GemNode node)
    {
        SetRanks();
        hero.OnDamageDealt += (in dmg) =>
        {
            if (_rush != null && dmg.Target is { } target && target.HealthFraction <= _rushThreshold) hero.Auras.Apply(_rush, dmg.Timestamp);
        };
    }

    private void BindVirtuoso(Hero hero, GemNode node)
    {
        SetRanks();
        hero.SpiritOfHeroismStarted += ctx =>
        {
            if (_essence != null) hero.Auras.Remove(_essence, ctx.CurrentTime);
        };
        hero.SpiritOfHeroismEnded += ctx =>
        {
            if (_essence != null) hero.Auras.Apply(_essence, ctx.CurrentTime);
        };
    }

    private void SetRanks()
    {
        _rush = NodeInSlot(1)?.Grants;
        _rushThreshold = NodeInSlot(1)?.Value("HealthThreshold") ?? 0.0;
        _essence = NodeInSlot(5)?.Grants;
    }

    protected override void BindLater(Hero hero, GemNode node, SimTime now)
    {
        if (node.Slot == 1) BindAdrenalineRush(hero, node);
        if (node.Slot == 5) BindVirtuoso(hero, node);
    }

    /// <summary>
    /// Each activation of slot 5 gives the Essence unless Spirit of Heroism is running. The node's augmentation
    /// (DT_GemDefinitions, Topaz row, CA_GemBonus_HastePercentageBonusWhileNotHeroismSmall) holds effect 1937, Infinite, with
    /// OngoingTagRequirements IgnoreTags CharacterStatus.SpiritBuffed, and no field limits it to one activation. Every
    /// Anomalous Residue: Topaz window of T2qLPpmMkrAtJ9B1 fight 18 (383.829, 530.946, 1400.233) and Fc8v6yWxD14GQMnV
    /// fight 19 (809.379, 958.352, 1120.143) has effect 1937 in the Residue's frame.
    /// </summary>
    protected override void NodesChanged(Hero hero, IReadOnlyList<GemNode> before, SimTime? now)
    {
        SetRanks();
        if (now is { } time && _essence != null && before.All(n => n.Slot != 5) && !hero.Auras.HasAura(Hero.SpiritOfHeroism, time))
            hero.Auras.Apply(_essence, time);
    }

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.HasteRating, node.Value("HasteBonus"))],
        4 => [StatModifier.Add(StatType.Haste, node.Value("HasteBonus"))],
        _ => [],
    };
}
