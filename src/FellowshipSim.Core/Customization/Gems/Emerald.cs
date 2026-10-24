namespace FellowshipSim.Core.Customization.Gems;

public class Emerald : Gem
{
    public const int FirstStrikeEffectId = 1943;
    public const int FirstStrikeIIEffectId = 1944;
    public const double FirstStrikeSeconds = 15.0;

    public static readonly GemNode[] AllNodes =
    [
        new(322, 1, "First Strike", 80, V(("ExpertiseBonus", 0.05), ("Duration", FirstStrikeSeconds)),
            AuraDef.Effect("First Strike", FirstStrikeEffectId, FirstStrikeSeconds, modifiers: StatModifier.Add(StatType.Expertise, 0.05))),
        new(282, 2, "Vanguard's Resolve", 150, V(("StaminaBonus", 4), ("ExpertiseBonus", 8))),
        new(346, 3, "Sentinel's Bastion", 220, V(("Cooldown", 60), ("ShieldPercent", 0.05), ("ShieldDuration", 60))),
        new(328, 4, "Tactician's Accumen", 300, V(("ExpertiseBonus", 0.02))),
        new(342, 5, "Blessing of the Commander", 450, V(("CooldownReduction", 0.04))),
        new(323, 6, "First Strike - II", 600, V(("ExpertiseBonus", 0.15), ("Duration", FirstStrikeSeconds)),
            AuraDef.Effect("First Strike - II", FirstStrikeIIEffectId, FirstStrikeSeconds, modifiers: StatModifier.Add(StatType.Expertise, 0.15))),
        new(283, 7, "Vanguard's Resolve - II", 800, V(("StaminaBonus", 12), ("ExpertiseBonus", 24))),
        new(347, 8, "Sentinel's Bastion - II", 1000, V(("Cooldown", 60), ("ShieldPercent", 0.15), ("ShieldDuration", 60))),
        new(329, 9, "Tactician's Accumen - II", 1250, V(("ExpertiseBonus", 0.06))),
        new(343, 10, "Blessing of the Commander - II", 1500, V(("CooldownReduction", 0.12))),
    ];

    /// <summary>The engine-internal marker that an enemy has already triggered First Strike.</summary>
    public static readonly AuraDef FirstStrikeTriggered = AuraDef.Internal("first_strike_triggered", 0);

    public override GemColor Color => GemColor.Emerald;
    protected override IReadOnlyList<GemNode> NodeTable => AllNodes;

    /// <summary>The First Strike aura of the unlocked rank, if any (Emerald Judgement also triggers it).</summary>
    public AuraDef? FirstStrike { get; private set; }

    protected override bool BindNode(Hero hero, GemNode node)
    {
        switch (node.Slot)
        {
            case 1: // First Strike: Expertise for 15s when dealing damage to an enemy for the first time.
                BindFirstStrike(hero, node);
                return true;
            case 2: // Vanguard's Resolve: flat Stamina/Expertise rating (in the sheet ratings).
                return true;
            case 4: // Tactician's Accumen: Expertise.
                hero.Stats.BonusExpertise += node.Value("ExpertiseBonus");
                return true;
            case 5: // Blessing of the Commander: ability cooldown reduction.
                hero.Stats.CooldownReductionPct += node.Value("CooldownReduction");
                return true;
            default: // Sentinel's Bastion (shield) does not affect damage.
                return true;
        }
    }

    private void BindFirstStrike(Hero hero, GemNode node)
    {
        FirstStrike = hero.Register(node.Grants!);
        hero.OnDamageDealt += (in dmg) =>
        {
            if (FirstStrike is not { } strike || dmg.Target == null || dmg.Target.Auras.HasAura(FirstStrikeTriggered, dmg.Timestamp)) return;
            dmg.Target.Auras.Apply(FirstStrikeTriggered, dmg.Timestamp);
            hero.Auras.Apply(strike, dmg.Timestamp);
        };
    }

    protected override void BindLater(Hero hero, GemNode node, SimTime now)
    {
        if (node.Slot == 1) BindFirstStrike(hero, node);
    }

    protected override void NodesChanged(Hero hero, IReadOnlyList<GemNode> before, SimTime? now) =>
        FirstStrike = NodeInSlot(1)?.Grants is { } strike ? hero.Register(strike) : null;

    protected override IEnumerable<StatModifier> StatEffects(GemNode node) => node.Slot switch
    {
        2 => [StatModifier.Add(StatType.ExpertiseRating, node.Value("ExpertiseBonus"))],
        4 => [StatModifier.Add(StatType.Expertise, node.Value("ExpertiseBonus"))],
        _ => [],
    };
}
