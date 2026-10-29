namespace FellowshipSim.Core.Customization.Gems;

public class Amethyst : Gem
{
    public static readonly GemNode[] AllNodes =
    [
        new(531, 1, "Sealed Fate", 80, V(("HealthThreshold", 0.5), ("CritChanceBonus", 0.05))),
        new(284, 2, "Berserker's Zeal", 150, V(("StaminaBonus", 4), ("CritBonus", 8))),
        new(306, 3, "Reaper's Reprieve", 220, V(("HealPercent", 0.12), ("Duration", 18))),
        new(332, 4, "Killer Instinct", 300, V(("CritChanceBonus", 0.02))),
        new(334, 5, "Blessing of the Deathdealer", 450, V(("CritPowerBonus", 0.02))),
        new(532, 6, "Sealed Fate - II", 600, V(("HealthThreshold", 0.5), ("CritChanceBonus", 0.15))),
        new(285, 7, "Berserker's Zeal - II", 800, V(("StaminaBonus", 12), ("CritBonus", 24))),
        new(307, 8, "Reaper's Reprieve - II", 1000, V(("HealPercent", 0.36), ("Duration", 18))),
        new(333, 9, "Killer Instinct - II", 1250, V(("CritChanceBonus", 0.06))),
        new(335, 10, "Blessing of the Deathdealer - II", 1500, V(("CritPowerBonus", 0.06))),
    ];

    public override GemColor Color => GemColor.Amethyst;
    protected override IReadOnlyList<GemNode> NodeTable => AllNodes;

    protected override bool BindNode(Hero hero, GemNode node)
    {
        switch (node.Slot)
        {
            case 1: // Sealed Fate: extra crit chance against characters above a health threshold.
                BindSealedFate(hero, node);
                return true;
            case 2: // Berserker's Zeal: flat Stamina and Crit rating.
                hero.Stats.CritRating += node.Value("CritBonus");
                hero.Stats.Stamina += node.Value("StaminaBonus");
                return true;
            case 4: // Killer Instinct: crit chance.
                hero.Stats.BonusCritChance += node.Value("CritChanceBonus");
                return true;
            case 5: // Blessing of the Deathdealer: critical strikes do more damage.
                hero.PassiveAuras.Add(AuraDef.Internal(node.Name, 0, 1,
                    StatModifier.More(StatType.CritMultiplier, 1.0 + node.Value("CritPowerBonus"))));
                return true;
            default: // Reaper's Reprieve (healing) does not affect damage.
                return true;
        }
    }

    // Sealed Fate of the active rank (none: no bonus).
    private double _sealedFateThreshold, _sealedFateBonus;

    private void BindSealedFate(Hero hero, GemNode node)
    {
        SetSealedFate(node);
        hero.TargetCritChanceBonuses.Add((target, _) => target.HealthFraction > _sealedFateThreshold ? _sealedFateBonus : 0.0);
    }

    private void SetSealedFate(GemNode? node)
    {
        _sealedFateThreshold = node?.Value("HealthThreshold") ?? 1.0;
        _sealedFateBonus = node?.Value("CritChanceBonus") ?? 0.0;
    }

    protected override void BindLater(Hero hero, GemNode node, SimTime now)
    {
        if (node.Slot == 1) BindSealedFate(hero, node);
    }

    protected override void NodesChanged(Hero hero, IReadOnlyList<GemNode> before, SimTime? now) => SetSealedFate(NodeInSlot(1));

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.CritRating, node.Value("CritBonus"))],
        4 => [StatModifier.Add(StatType.CritChance, node.Value("CritChanceBonus"))],
        5 => [StatModifier.More(StatType.CritMultiplier, 1.0 + node.Value("CritPowerBonus"))],
        _ => [],
    };
}
