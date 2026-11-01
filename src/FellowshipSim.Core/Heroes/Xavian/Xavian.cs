using FellowshipSim.Core.Heroes.Xavian.Abilities;
using FellowshipSim.Core.Heroes.Xavian.Talents;

namespace FellowshipSim.Core.Heroes.Xavian;

/// <summary>
/// Xavian, the Light of Azamir. His abilities spend Mana, which regenerates. Sun Strike, Blinding Slash, Omnistrike and
/// Solar Blades grant Swift Reprieval, which turns Brilliant Flare into the instant Brilliant Flash; Omega Reprieval does
/// the same with an enhanced Flash. Abilities live in Abilities/, talents in Talents/.
/// </summary>
public sealed class Xavian : Hero
{
    /// <summary>Hero 25 attributes MaxMana 1440 and Mana 1440: every fight starts with full Mana.</summary>
    public const double MaxMana = 1440;

    /// <summary>Hero 25 attribute ManaRegenRate 0.0005: a share of the maximum Mana every <see cref="ManaTickSeconds"/> in combat.</summary>
    public const double ManaRegenRate = 0.0005;

    /// <summary>Hero 25 attribute OutOfCombatManaRegenRate 0.0036, every <see cref="ManaTickSeconds"/> out of combat.</summary>
    public const double OutOfCombatManaRegenRate = 0.0036;

    /// <summary>Hero 25 attributes ManaRegenTickTime and OutOfCombatManaRegenTickTime 0.1.</summary>
    public const double ManaTickSeconds = 0.1;

    /// <summary>
    /// Effect 2662 (Xavian's Spirit of Heroism): ManaRegenRate x3 and ManaRegenTickTime x0.5, so 6 times the Mana every
    /// second.
    /// </summary>
    public const double SpiritOfHeroismManaRegen = 6.0;

    /// <summary>
    /// Effect 2662 "Spirit of Heroism" (GE_Sune_SpiritTriggeredBuff): "Haste is increased by 30%. Mana regeneration is
    /// increased by 600%." Combat logs (16 Xavian players) give Xavian this effect from Decree of the Sun, never effect
    /// 2253, and keep it 38s with Sapphire's Blessing of the Prophet - II (20s and 18s).
    /// </summary>
    public static readonly AuraDef SpiritOfHeroismBuff = AuraDef.Effect("Spirit of Heroism", 2662, SpiritOfHeroismSeconds,
        modifiers: StatModifier.Add(StatType.Haste, SpiritOfHeroismHaste)) with { KeepsOnDeath = true };

    /// <summary>
    /// Hero 25 attribute MaxResources 3: the Swift Reprieval stacks held at most. Combat logs (XyGpcHjNaAzgk7mW fight 20,
    /// gd6ByNrf81jc9aWw fight 3): 117 gains to 3 stacks, none past it.
    /// </summary>
    public const int MaxSwiftReprievals = 3;

    public static readonly ResourceDef Mana = new("Mana", Max: MaxMana, Initial: MaxMana);

    /// <summary>
    /// Effect 2663: "Your Brilliant Flare turns into Brilliant Flash, causing it to be instant cast, have no Mana cost ...
    /// Brilliant Flash requires and consumes 1 stack of Swift Reprieval." Combat logs show it as an aura with stacks.
    /// </summary>
    public static readonly AuraDef SwiftReprieval = AuraDef.Effect("Swift Reprieval", 2663, maxStacks: MaxSwiftReprievals);

    /// <summary>Class tuning: hero 25 attribute BalancingDamageAura 1, none.</summary>
    public const double BalancingDamageAura = 1.0;

    /// <summary>Xavian's talent tree, as in game (game data talent-slot records, hero Xavian).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                               b                          c
        TalentSlot.InRow(1, 2,    SunsTouch.TalentId,             GrandHalo.TalentId,        SolarBurn.TalentId),
        TalentSlot.InRow(2, 1,    TemperedDescent.TalentId,       LightsOath.TalentId,       RisingSun.TalentId),
        TalentSlot.InRow(3, 2,    ImposingPresence.TalentId,      CelestialFavor.TalentId,   GleamingStrikes.TalentId),
        TalentSlot.InRow(4, 1,    SunsVerdict.TalentId,           MagicWard.TalentId,        HallowedShield.TalentId),
        TalentSlot.InRow(5, 3,    VanguardOfVengeance.TalentId,   Invictus.TalentId,         GoldenHour.TalentId),
        TalentSlot.InRow(6, 1,    ZenithTechnique.TalentId,       HealingHalo.TalentId,      ShiftingSpectrum.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Datamine hero 25: mainStat Strength.</summary>
    public override MainStat MainStat => MainStat.Strength;

    public double ManaHeld => ResourceAmount(Mana);

    /// <summary>Swift Reprieval stacks held.</summary>
    public int SwiftReprievals(SimTime now) => Auras.GetStacks(SwiftReprieval, now);

    /// <summary>
    /// An Offensive Ability resolved (Golden Hour's trigger): Sun Strike, Blinding Slash, Solar Blades, Omnistrike,
    /// Brilliant Flash on an enemy, Shining Halo and Ruptured Dawn.
    /// </summary>
    public event Action<SimulationContext, Ability>? OffensiveAbilityUsed;

    internal void RaiseOffensiveAbilityUsed(SimulationContext ctx, Ability ability) => OffensiveAbilityUsed?.Invoke(ctx, ability);

    public override void Initialize()
    {
        AddResource(Mana);
        SpiritRefundResource = Mana;
        ClassDamageTuning = BalancingDamageAura;

        Abilities.Add(new Attack(this));
        Abilities.Add(new SunStrike(this));
        Abilities.Add(new BlindingSlash(this));
        Abilities.Add(SolarBlades = new SolarBlades(this));
        Abilities.Add(new Omnistrike(this));
        Abilities.Add(new BrilliantFlare(this));
        Abilities.Add(BrilliantFlash = new BrilliantFlash(this));
        Abilities.Add(new OmegaReprieval(this));
        Abilities.Add(ShiningHalo = new ShiningHalo(this));
        Abilities.Add(SolarShield = new SolarShield(this));
        Abilities.Add(AuraOfSolace = new AuraOfSolace(this));
        Abilities.Add(new SkyCrash(this));
        Abilities.Add(new RupturedDawn(this));
        Abilities.Add(new DecreeOfTheSun(this));
        Abilities.Add(new Taunt(this));
        Abilities.Add(new Interrupt(this));
    }

    public SolarBlades SolarBlades { get; private set; } = null!;
    public BrilliantFlash BrilliantFlash { get; private set; } = null!;
    public ShiningHalo ShiningHalo { get; private set; } = null!;
    public SolarShield SolarShield { get; private set; } = null!;
    public AuraOfSolace AuraOfSolace { get; private set; } = null!;

    /// <summary>Grants one Swift Reprieval stack.</summary>
    public void GrantSwiftReprieval(SimulationContext ctx) => Auras.Apply(SwiftReprieval, ctx.CurrentTime);

    /// <summary>Adds Mana (from <paramref name="target"/>, if a hit on it earned it).</summary>
    public void GainMana(double amount, SimulationContext ctx, EnemyTarget? target = null) => GainResource(Mana, amount, ctx, target);

    /// <summary>Spends Mana (a Spirit Refund may give it back).</summary>
    public void SpendMana(double amount, SimulationContext ctx) => SpendResource(Mana, amount, ctx);

    /// <summary>Takes Mana with no ability cast behind it (Aura of Solace's upkeep), so no Spirit Refund. UNVERIFIED.</summary>
    public void DrainMana(double amount, SimulationContext ctx)
    {
        var state = Resources.Get(Mana);
        state.Current = Math.Max(0.0, state.Current - amount);
        NotifyResourceSpent(Mana, amount, ctx.CurrentTime);
    }

    /// <summary>
    /// "Xavian's Brilliant Flash ability has a chance equal to your Spirit to trigger a Spirit Refund even though it does
    /// not have a Mana cost. When it does, you instantly gain 50 Mana." The roll is the Spirit Refund's own.
    /// </summary>
    public bool RollBrilliantSpirit(SimulationContext ctx) => TriggerSpiritRefund(null, 0.0, ctx);

    /// <summary>
    /// Mana every second: the maximum times <see cref="ManaRegenRate"/> each tick, raised by Spirit ("Xavian's passive
    /// Mana Generation is increased by an amount equal to your Spirit") and by Spirit of Heroism.
    /// </summary>
    public double ManaPerSecond(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        double rate = ctx.ActiveEnemies.Count > 0 ? ManaRegenRate : OutOfCombatManaRegenRate;
        double perSecond = MaxMana * rate / ManaTickSeconds * (1.0 + SpiritPercent(now));
        if (Auras.HasAura(SpiritOfHeroismBuff, now)) perSecond *= SpiritOfHeroismManaRegen;
        return perSecond;
    }

    protected override void OnFightBegins(SimulationContext ctx)
    {
        void Tick(SimulationContext c)
        {
            GainMana(ManaPerSecond(c) * ManaTickSeconds, c);
            c.Schedule(SimTime.FromSeconds(ManaTickSeconds), Tick, "Mana regeneration");
        }
        ctx.Schedule(SimTime.FromSeconds(ManaTickSeconds), Tick, "Mana regeneration");
    }

    public override void ResetForIteration()
    {
        ResetCombatState();
        AuraOfSolace.ResetState();
        ShiningHalo.ResetState();
    }
}
