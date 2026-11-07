using FellowshipSim.Core.Heroes.Helena.Abilities;
using FellowshipSim.Core.Heroes.Helena.Talents;

namespace FellowshipSim.Core.Heroes.Helena;

/// <summary>
/// Helena, the Warmaster (hero 22), a Strength tank. Veteran of War: her abilities take time off each other's cooldowns
/// (<see cref="VeteranOfWar"/>). Toughness is her resource: Shield Slam, Shockwave and Shields Up raise it, and a replay
/// takes it from the log. Abilities are in Abilities/ and talents in Talents/.
/// </summary>
public sealed class Helena : Hero
{
    /// <summary>
    /// Toughness, as a share of its maximum (0 to 100). Combat logs give it as resource type 3: the amount and the maximum
    /// in hundredths. The maximum follows Strength and the secondary stats ("equal to 770% strength. Every 1% of Critical
    /// Strike, Haste, Expertise, and Spirit you have further increases the final amount by 2%", ability 977).
    /// </summary>
    public static readonly ResourceDef Toughness = new("Toughness", Max: 100);

    /// <summary>Class tuning: hero 22 baseAttributes BalancingDamageAura 1 (entities.jsonl v26320967), none.</summary>
    public const double BalancingDamageAura = 1.0;

    /// <summary>Health before Stamina (game data, Helena baseAttributes BaseHealth).</summary>
    public const double BaseHealth = 2556;
    /// <summary>Health per point of Stamina (game data, Helena baseAttributes StaminaToMaxHealthMultiplier).</summary>
    public const double StaminaToMaxHealthMultiplier = 76.9365;

    /// <summary>
    /// Spirit Points per Spirit Refund: "Each time you trigger the Spirit Refund you gain 1.2 Spirit Points toward your
    /// Spirit Ability charge" (hero 22 spiritAbility, entities.jsonl v26320967).
    /// </summary>
    public const double SpiritPointsPerRefund = 1.2;

    /// <summary>
    /// Veteran of War takes twice the seconds each tooltip names off its targets. Combat logs (18 fights of patch
    /// 0.5.0.0): with the tooltips' seconds the replay finds Shockwave on cooldown at 718 of its logged casts, Shield Throw at
    /// 173 and Shield Slam at 112. With twice them, Shockwave is on cooldown at 3 and Shield Slam at 14. Shield Throw is on
    /// cooldown at one logged cast each in D1fBhVPkwYKXWbpr fight 1, HgamNcX24tJ3qVQA fight 21 and mThk4v1f8WGbBd7Q fight 1,
    /// with 0.12 to 0.38 s left.
    /// </summary>
    public const double VeteranOfWarTooltipMultiplier = 2.0;

    /// <summary>
    /// Veteran of War's reductions while Siegebreaker is up: "the cooldown reduction granted by your Veteran of War is
    /// doubled" (effect 2192).
    /// </summary>
    public const double SiegebreakerVeteranOfWarMultiplier = 2.0;

    /// <summary>
    /// Effect 1350 "Spirit of Heroism": "Haste is increased by 30%", Haste +0.3 for 20s (codex). Combat logs (19 fights): 270
    /// Siegebreaker casts and 240 applications of effect 1350, none of effect 2253.
    /// </summary>
    public const int SiegebreakerHeroismEffectId = 1350;

    public static readonly AuraDef SpiritOfHeroismBuff = AuraDef.Effect("Spirit of Heroism", SiegebreakerHeroismEffectId, SpiritOfHeroismSeconds,
        modifiers: StatModifier.Add(StatType.Haste, SpiritOfHeroismHaste)) with { KeepsOnDeath = true };

    public override AuraDef SpiritOfHeroismAura => SpiritOfHeroismBuff;

    /// <summary>Helena's talent tree, as in game (game data talent-slot records, hero Helena).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                            b                            c
        TalentSlot.InRow(1, 2,    TheBestDefense.TalentId,     ShieldMastery.TalentId,      SwordAndBoard.TalentId),
        TalentSlot.InRow(2, 1,    LingeringRush.TalentId,      GuardedVeteran.TalentId,     PunishingStrikes.TalentId),
        TalentSlot.InRow(3, 2,    Aftershock.TalentId,         SharpenedBlade.TalentId,     UnscarredSoul.TalentId),
        TalentSlot.InRow(4, 1,    HighCommand.TalentId,        Defiance.TalentId,           SkullCracker.TalentId),
        TalentSlot.InRow(5, 3,    SecondWind.TalentId,         MartialCommand.TalentId,     GleamingShield.TalentId),
        TalentSlot.InRow(6, 1,    FrontLineDefender.TalentId,  MasterOfWar.TalentId,        GreaterShockwave.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Datamine hero 22: mainStat Strength.</summary>
    public override MainStat MainStat => MainStat.Strength;

    /// <summary>
    /// Multipliers on every Veteran of War reduction of one cast, read as the cast resolves (Punishing Strikes). Siegebreaker
    /// is in <see cref="VeteranOfWar"/> itself.
    /// </summary>
    public List<Func<SimulationContext, HelenaAbility, double>> VeteranOfWarMultipliers { get; } = new();

    /// <summary>Shares of Armor added to it while they hold (Gleaming Shield), added together.</summary>
    public List<Func<SimTime, double>> ArmorBonuses { get; } = new();

    /// <summary>Armor now, for damage that reads it: the build's Armor with <see cref="ArmorBonuses"/>.</summary>
    public double Armor(SimTime now)
    {
        double bonus = 0.0;
        foreach (var b in ArmorBonuses) bonus += b(now);
        return Stats.Armor * (1.0 + bonus);
    }

    /// <summary>A Veteran of War source cast took time off its targets' cooldowns.</summary>
    public event Action<SimulationContext, HelenaAbility>? VeteranOfWarApplied;

    public override void Initialize()
    {
        AddResource(Toughness);
        ClassDamageTuning = BalancingDamageAura;
        Stats.BaseHealth = BaseHealth;
        Stats.StaminaToMaxHealth = StaminaToMaxHealthMultiplier;
        SpiritPointsPerSpiritRefund = SpiritPointsPerRefund;

        Abilities.Add(new Attack(this));
        Abilities.Add(new ShieldSlam(this));
        Abilities.Add(new Shockwave(this));
        Abilities.Add(new ShieldThrow(this));
        Abilities.Add(new PowerStrike(this));
        Abilities.Add(new MeasuredStrike(this));
        Abilities.Add(new SweepingStrike(this));
        Abilities.Add(new SweepingStrikeBleed(this));
        Abilities.Add(new HoldTheLine(this));
        Abilities.Add(new ShieldsUp(this));
        Abilities.Add(new Charge(this));
        Abilities.Add(new Bash(this));
        Abilities.Add(new Taunt(this));
        Abilities.Add(new IronWall(this));
        Abilities.Add(new GrandMelee(this));
        Abilities.Add(new Siegebreaker(this));
    }

    /// <summary>Toughness now, as a share of its maximum (0 to 1).</summary>
    public double ToughnessShare => ResourceAmount(Toughness) / Toughness.Max;

    /// <summary>
    /// The maximum Toughness over 770% of Strength: "Every 1% of Critical Strike, Haste, Expertise, and Spirit you have
    /// further increases the final amount by 2%" (ability 977).
    /// </summary>
    public double ToughnessStatMultiplier(SimTime now) =>
        1.0 + 2.0 * (CritChance(now) + GetHaste(now) + Expertise(now) + SpiritPercent(now));

    /// <summary>
    /// Gains Toughness worth <paramref name="strengthShare"/> of the 770% of Strength the maximum is built on (the game
    /// data's "generates ... shareOfMaximum"): a smaller share of a maximum the secondary stats have raised.
    /// </summary>
    public void GainToughness(double strengthShare, SimulationContext ctx) =>
        GainResource(Toughness, strengthShare / ToughnessStatMultiplier(ctx.CurrentTime) * Toughness.Max, ctx);

    /// <summary>
    /// Veteran of War: <paramref name="source"/>'s cast takes its reductions off its targets' cooldowns, doubled while
    /// Siegebreaker is up, and by <see cref="VeteranOfWarMultipliers"/>. The cast rolls its Spirit Refund.
    /// </summary>
    internal void VeteranOfWar(SimulationContext ctx, HelenaAbility source)
    {
        if (source.VeteranOfWarReductions.Count == 0) return;
        var now = ctx.CurrentTime;
        double multiplier = VeteranOfWarTooltipMultiplier;
        if (Auras.HasAura(Siegebreaker.Buff, now)) multiplier *= SiegebreakerVeteranOfWarMultiplier;
        foreach (var bonus in VeteranOfWarMultipliers) multiplier *= bonus(ctx, source);
        TriggerSpiritRefund(null, 0.0, ctx);
        foreach (var (target, seconds) in source.VeteranOfWarReductions)
        {
            if (AbilityOfType(target) is { } ability) CooldownOf(ability).Reduce(SimTime.FromSeconds(seconds * multiplier), now);
        }
        VeteranOfWarApplied?.Invoke(ctx, source);
    }

    private Ability? AbilityOfType(Type type) => Abilities.FirstOrDefault(a => a.GetType() == type);

    public override void ResetForIteration() => ResetCombatState();
}
