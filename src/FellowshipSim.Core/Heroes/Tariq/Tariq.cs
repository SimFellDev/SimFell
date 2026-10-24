using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Core.Heroes.Tariq;

/// <summary>
/// Tariq, the Thunderlord. His generators build Fury; Skull Crusher, Hammer Storm and Culling Strike spend it. Thunder
/// Call (and Raging Tempest's Enhanced Thunder Call) charges his abilities with lightning and opens Chain Lightning.
/// Abilities live in Abilities/, talents in Talents/.
/// </summary>
public sealed class Tariq : Hero
{
    /// <summary>
    /// Fury (hero 10 Attributes MaxResources 100). Combat logs report it as resource type 2 in raw units of a maximum
    /// that follows Strength (effect 1735 overrides MaxResources from Strength); every gain in the game data is a share of
    /// that maximum, so 1 Fury is 1% of it.
    /// </summary>
    public static readonly ResourceDef Fury = new("Fury", Max: 100);

    /// <summary>Class tuning: hero 10 attribute BalancingDamageAura 1, none.</summary>
    public const double BalancingDamageAura = 1.0;

    /// <summary>Spirit Points per Spirit Refund (ability 1466, Ink.SpiritProc.ResourceRefund.FlatSpiritPointGain 1).</summary>
    public const double SpiritPointsPerRefund = 1.0;

    /// <summary>Tariq's talent tree, as in game (game data talent-slot records, hero Tariq).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                          b                          c
        TalentSlot.InRow(1, 2,    LeftHandPath.TalentId,     RideTheLightning.TalentId, SquareHammer.TalentId),
        TalentSlot.InRow(2, 1,    HighRoad.TalentId,         BloodAndThunder.TalentId,  Bloodline.TalentId),
        TalentSlot.InRow(3, 2,    TheMotherload.TalentId,    MouthForWar.TalentId,      Thunderstruck.TalentId),
        TalentSlot.InRow(4, 1,    Pneuma.TalentId,           Sledgehammer.TalentId,     FarBeyondDriven.TalentId),
        TalentSlot.InRow(5, 3,    KillEmAll.TalentId,        AceOfSpades.TalentId,      Schism.TalentId),
        TalentSlot.InRow(6, 1,    ThemBones.TalentId,        CrackTheSky.TalentId,      KillingInTheName.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Extra crit chance on Tariq's lightning damage (Crack the Sky).</summary>
    public double LightningCritChance { get; internal set; }

    /// <summary>Fury gained every second of the fight (Bloodline).</summary>
    public double FuryPerSecond { get; internal set; }

    /// <summary>
    /// Thunder Call, or Raging Tempest's Enhanced Thunder Call: abilities add their lightning and Chain Lightning can be
    /// cast. Combat logs (AjVzXy7pRmJd1BKa fight 17, FellowshipAnalyzer corpus): Raging Tempest alone enables both.
    /// </summary>
    public bool IsCharged(SimTime now) => Auras.HasAura(ThunderCall.Buff, now) || Auras.HasAura(RagingTempest.Buff, now);

    public override void Initialize()
    {
        AddResource(Fury);
        SpiritRefundResource = Fury;
        ClassDamageTuning = BalancingDamageAura;
        SpiritPointsPerSpiritRefund = SpiritPointsPerRefund;

        Abilities.Add(new WildSwing(this));
        Abilities.Add(new FaceBreaker(this));
        Abilities.Add(new HeavyStrike(this));
        Abilities.Add(new SkullCrusher(this));
        Abilities.Add(new HammerStorm(this));
        Abilities.Add(new CullingStrike(this));
        Abilities.Add(new ChainLightning(this));
        Abilities.Add(new LeapSmash(this));
        Abilities.Add(new ThunderCall(this));
        Abilities.Add(new FocusedWrath(this));
        Abilities.Add(new RagingTempest(this));
    }

    /// <summary>Adds Fury (from <paramref name="target"/>, if a hit on it earned it).</summary>
    public void GainFury(double amount, SimulationContext ctx, EnemyTarget? target = null) => GainResource(Fury, amount, ctx, target);

    /// <summary>Spends Fury (a Spirit Refund may give it back).</summary>
    public void SpendFury(double amount, SimulationContext ctx) => SpendResource(Fury, amount, ctx);

    protected override void OnFightBegins(SimulationContext ctx)
    {
        if (FuryPerSecond <= 0) return;
        // Bloodline (talent 76, effect 2424): 1% of maximum Fury every hasted second in combat (see Bloodline).
        void Regen(SimulationContext c)
        {
            if (c.ActiveEnemies.Count > 0) GainFury(FuryPerSecond, c);
            HastedTimer.Start(c, this, Bloodline.PeriodSeconds, Regen, Bloodline.TalentName);
        }
        HastedTimer.Start(ctx, this, Bloodline.PeriodSeconds, Regen, Bloodline.TalentName);
    }

    public override void ResetForIteration() => ResetCombatState();
}
