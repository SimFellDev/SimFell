namespace FellowshipSim.Core.Customization.Gems;

/// <summary>The Topaz gem tree. Node values are from the game data (gem-trait records).</summary>
public class Topaz : Gem
{
    public const double AdrenalineRushSeconds = 10.0;

    public static readonly GemNode[] AllNodes =
    [
        new(318, 1, "Adrenaline Rush", 80, V(("HasteBonus", 0.03), ("Duration", AdrenalineRushSeconds), ("HealthThreshold", 0.3)),
            AuraDef.Effect("Adrenaline Rush", 1939, AdrenalineRushSeconds, modifiers: StatModifier.Add(StatType.Haste, 0.03))),
        new(280, 2, "Thief's Alacrity", 150, V(("StaminaBonus", 8), ("HasteBonus", 8))),
        new(344, 3, "Rogue's Resurgence", 220, V(("HealthThreshold", 0.5), ("HealPercent", 0.08), ("Cooldown", 20))),
        new(320, 4, "Feline Grace", 300, V(("HasteBonus", 0.02))),
        new(316, 5, "Blessing of the Virtuoso", 450, V(("HasteBonus", 0.02)),
            AuraDef.Effect("Essence of the Virtuoso", 1937, modifiers: StatModifier.Add(StatType.Haste, 0.02))),
        new(319, 6, "Adrenaline Rush - II", 600, V(("HasteBonus", 0.09), ("Duration", AdrenalineRushSeconds), ("HealthThreshold", 0.3)),
            AuraDef.Effect("Adrenaline Rush - II", 1940, AdrenalineRushSeconds, modifiers: StatModifier.Add(StatType.Haste, 0.09))),
        new(281, 7, "Thief's Alacrity - II", 800, V(("StaminaBonus", 24), ("HasteBonus", 24))),
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
                var rush = node.Grants!;
                double threshold = node.Value("HealthThreshold");
                hero.OnDamageDealt += (in dmg) =>
                {
                    if (dmg.Target is { } target && target.HealthFraction <= threshold) hero.Auras.Apply(rush, dmg.Timestamp);
                };
                return true;
            case 4: // Feline Grace: Haste.
                hero.Stats.BonusHaste += node.Value("HasteBonus");
                return true;
            case 5: // Blessing of the Virtuoso: "You retain X Haste from Spirit of Heroism while it is not active."
                var essence = node.Grants!;
                hero.PassiveAuras.Add(essence);
                hero.SpiritOfHeroismStarted += ctx => hero.Auras.Remove(essence, ctx.CurrentTime);
                hero.SpiritOfHeroismEnded += ctx => hero.Auras.Apply(essence, ctx.CurrentTime);
                return true;
            default: // Thief's Alacrity (rating, in the logged ratings) and Rogue's Resurgence (healing).
                return true;
        }
    }
}
