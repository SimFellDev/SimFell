namespace FellowshipSim.Core.Customization.Gems;

/// <summary>The Diamond gem tree. Node values are from the game data (gem-ability records).</summary>
public class Diamond : Gem
{
    public const double HarmoniousSoulSeconds = 5.0;
    public const double HarmoniousSoulStatPerStack = 0.006;
    public const int HarmoniousSoulSmallEffectId = 1959;
    public const int HarmoniousSoulMediumEffectId = 1960;

    /// <summary>
    /// Effect 1959 "Harmounious Soul - Small" (the game's spelling), applied by node 338's ability 1930: +0.6% Critical
    /// Strike, Haste, Expertise and Spirit per stack (modifiers CritChance, Haste, Expertise and Spirit, Additive 0.006),
    /// 5s, 4 stacks. When its duration expires it loses one stack. Combat logs (4Ln6PQZCRzc7Wpta fight 78, Rzg1nQNHpm2fhYtB
    /// fight 23): each loss restarts the 5s (removebuffstack every 5.01s).
    /// </summary>
    public static readonly AuraDef HarmoniousSoulSmall = HarmoniousSoulAura("Harmounious Soul - Small", HarmoniousSoulSmallEffectId, 4);

    /// <summary>Effect 1960 "Harmounious Soul - Medium", applied by node 339's ability 1931: as 1959 with 12 stacks.</summary>
    public static readonly AuraDef HarmoniousSoulMedium = HarmoniousSoulAura("Harmounious Soul - Medium", HarmoniousSoulMediumEffectId, 12);

    private static AuraDef HarmoniousSoulAura(string name, int effectId, int maxStacks) =>
        AuraDef.Effect(name, effectId, HarmoniousSoulSeconds, maxStacks,
            StatModifier.Add(StatType.CritChance, HarmoniousSoulStatPerStack),
            StatModifier.Add(StatType.Haste, HarmoniousSoulStatPerStack),
            StatModifier.Add(StatType.Expertise, HarmoniousSoulStatPerStack),
            StatModifier.Add(StatType.Spirit, HarmoniousSoulStatPerStack)) with { ExpiryRemovesOneStack = true };

    public static readonly GemNode[] AllNodes =
    [
        new(338, 1, "Harmonious Soul", 80, V(("StatBonus", HarmoniousSoulStatPerStack), ("Duration", HarmoniousSoulSeconds), ("MaxStacks", 4), ("StackLoss", 1))),
        new(298, 2, "Stoic's Teachings", 150, V(("ArmorBonus", 40), ("PrimaryStatBonus", 4))),
        new(302, 3, "Tranquil Resolve", 220, V(("DamageReduction", 0.08), ("Duration", 6), ("Cooldown", 20))),
        new(292, 4, "Ancient's Wisdom", 300, V(("PrimaryStatBonus", 1.02))),
        new(290, 5, "Blessing of the Artisan", 450, V(("CooldownReduction", 0.08))),
        new(339, 6, "Harmonious Soul - II", 600, V(("StatBonus", HarmoniousSoulStatPerStack), ("Duration", HarmoniousSoulSeconds), ("MaxStacks", 12), ("StackLoss", 1))),
        new(299, 7, "Stoic's Teachings - II", 800, V(("ArmorBonus", 120), ("PrimaryStatBonus", 12))),
        new(303, 8, "Tranquil Resolve - II", 1000, V(("DamageReduction", 0.24), ("Duration", 6), ("Cooldown", 20))),
        new(293, 9, "Ancient's Wisdom - II", 1250, V(("PrimaryStatBonus", 1.06))),
        new(291, 10, "Blessing of the Artisan - II", 1500, V(("CooldownReduction", 0.24))),
    ];

    public override GemColor Color => GemColor.Diamond;
    protected override IReadOnlyList<GemNode> NodeTable => AllNodes;

    /// <summary>The Harmonious Soul buff of the unlocked rank, if modeled (Diamond Strike reads its stacks).</summary>
    public AuraDef? HarmoniousSoul { get; private set; }

    protected override bool BindNode(Hero hero, GemNode node)
    {
        switch (node.Slot)
        {
            case 1: // Harmonious Soul: "Each time an enemy you are in combat with is defeated, you gain +0.6% Critical Strike,
                    // Haste, Expertise and Spirit for 5 seconds, stacking up to 4 (II: 12) times. When the duration of
                    // Harmonious Soul expires, you lose 1 stack."
                BindHarmoniousSoul(hero, node);
                return true;
            case 2: // Stoic's Teachings: flat Main Stat (a logged Main Stat already includes it) and Armor.
                if (!hero.Stats.UsesSheetValues) hero.Stats.PrimaryStat += node.Value("PrimaryStatBonus");
                return true;
            case 4: // Ancient's Wisdom: Main Stat (a logged Main Stat already includes it).
                // Not a node's Grants: a live power change moves its Main Stat through StatEffects, and a swapped grant would count twice.
                if (!hero.Stats.UsesSheetValues) hero.PassiveAuras.Add(hero.Register(AuraDef.Effect(node.Name, node.IsRankTwo ? 1918 : 1917, modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, node.Value("PrimaryStatBonus")))));
                return true;
            default: // Tranquil Resolve (defensive) and Blessing of the Artisan (Relic cooldowns) do not affect damage.
                return true;
        }
    }

    private static AuraDef SoulOf(GemNode node) => node.IsRankTwo ? HarmoniousSoulMedium : HarmoniousSoulSmall;

    private void BindHarmoniousSoul(Hero hero, GemNode node)
    {
        HarmoniousSoul = hero.Register(SoulOf(node));
        hero.OnEnemyDefeated += (ctx, _) =>
        {
            if (HarmoniousSoul is { } soul) hero.Auras.Apply(soul, ctx.CurrentTime);
        };
    }

    protected override void BindLater(Hero hero, GemNode node, SimTime now)
    {
        if (node.Slot == 1) BindHarmoniousSoul(hero, node);
    }

    /// <summary>The rank II Harmonious Soul takes over the stacks of the rank I one (the log's 1960 gains its stacks at once).</summary>
    protected override void NodesChanged(Hero hero, IReadOnlyList<GemNode> before, SimTime? now)
    {
        var old = HarmoniousSoul;
        HarmoniousSoul = NodeInSlot(1) is { } node ? hero.Register(SoulOf(node)) : null;
        if (now is not { } time || old == null || old == HarmoniousSoul || hero.Auras.Get(old, time) is not { } active) return;
        int stacks = active.Stacks;
        hero.Auras.Remove(old, time);
        if (HarmoniousSoul is { } soul) hero.Auras.Apply(soul, time, setStacks: Math.Min(stacks, soul.MaxStacks));
    }

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.PrimaryAttribute, node.Value("PrimaryStatBonus"))],
        4 => [StatModifier.More(StatType.PrimaryAttributeMultiplier, node.Value("PrimaryStatBonus"))],
        _ => [],
    };
}
