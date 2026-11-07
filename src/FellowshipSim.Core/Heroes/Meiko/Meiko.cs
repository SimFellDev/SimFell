using FellowshipSim.Core.Heroes.Meiko.Abilities;
using FellowshipSim.Core.Heroes.Meiko.Talents;

namespace FellowshipSim.Core.Heroes.Meiko;

/// <summary>
/// Meiko, the Twinsoul. Her Combo Builders (Wind Kick, Earth Fist, Spirit Palm) open her finishers: the first builder of a
/// combo picks the technique and the second picks the finisher. Rising Storm and Rising Earth grant the Stormfall and
/// Earthfall charges that Lashing Stormkick and Earthfist Barrage consume. Abilities live in Abilities/, talents in
/// Talents/.
/// </summary>
public sealed class Meiko : Hero
{
    /// <summary>
    /// Hero attribute MaxResourcesSecond 2 (game data hero 13): a combo holds 2 Combo Builders. Combat logs report no
    /// combo; the replay compares none.
    /// </summary>
    public const double MaxComboPoints = 2;

    /// <summary>
    /// Every Meiko ability's global cooldown: the builders' and Shatter Earth's globalCooldown 1, not hasted (game data
    /// abilities 1100, 1101, 1102, 1115). Combat logs (27 fights): 8,862 of the 11,816 gaps under 3s between builders and
    /// Shatter Earth are 1.03-1.07s.
    /// </summary>
    public const double GlobalCooldown = 1.0;

    /// <summary>Class tuning: hero 13 attribute BalancingDamageAura 1, none.</summary>
    public const double BalancingDamageAura = 1.0;

    /// <summary>Spirit Points per Spirit Refund (Meiko.SpiritProc.ResourceRefund.FlatSpiritPointGain 2).</summary>
    public const double SpiritPointsPerRefund = 2.0;

    /// <summary>
    /// Seconds after a Spirit Refund that the consumed charge comes back. Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 25.479):
    /// Lashing Stormkick consumes the last Stormfall and Stormfall returns at 25.681.
    /// </summary>
    public const double ChargeRefundDelaySeconds = 0.2;


    public static readonly ResourceDef ComboPoints = new("Combo Points", Max: MaxComboPoints)
    {
        Aliases = ["cp"],
    };

    /// <summary>The Kick Technique opened the combo held (Wind Kick was its first builder).</summary>
    public static readonly AuraDef KickTechnique = AuraDef.Internal("Kick Technique", 0);

    /// <summary>The Fist Technique opened the combo held (Earth Fist was its first builder).</summary>
    public static readonly AuraDef FistTechnique = AuraDef.Internal("Fist Technique", 0);

    /// <summary>The Palm Technique opened the combo held (Spirit Palm was its first builder).</summary>
    public static readonly AuraDef PalmTechnique = AuraDef.Internal("Palm Technique", 0);


    /// <summary>Meiko's talent tree, as in game (game data talent-slot records, hero Meiko).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                              b                              c
        TalentSlot.InRow(1, 2,    ResonanceOfEarth.TalentId,     StoneGuard.TalentId,           Earthwell.TalentId),
        TalentSlot.InRow(2, 1,    PerfectStorm.TalentId,         WillOfStone.TalentId,          WhippingApex.TalentId),
        TalentSlot.InRow(3, 2,    DebilitatingVortex.TalentId,   GuardiansFluidity.TalentId,    HarshWinds.TalentId),
        TalentSlot.InRow(4, 1,    Slipstream.TalentId,           Earthbourne.TalentId,          HarmonicResidual.TalentId),
        TalentSlot.InRow(5, 3,    WardenOfTheTemple.TalentId,    ShatteringObelisk.TalentId,    ForbiddenTechnique.TalentId),
        TalentSlot.InRow(6, 1,    Peacefield.TalentId,           RumblingStone.TalentId,        ConclusiveStrikes.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Datamine hero 13: mainStat Agility.</summary>
    public override MainStat MainStat => MainStat.Agility;

    /// <summary>The first and second Combo Builders of the combo held (null while the combo has none).</summary>
    public Technique? FirstBuilder { get; private set; }
    public Technique? SecondBuilder { get; private set; }

    /// <summary>A finisher was used on an enemy (Guardian's Fluidity, Harsh Winds).</summary>
    public event Action<SimulationContext, MeikoFinisher, EnemyTarget>? Finished;

    /// <summary>A Stormfall or Earthfall charge was consumed (Warden of the Temple).</summary>
    public event Action<SimulationContext, AuraDef>? ChargeConsumed;

    public override void Initialize()
    {
        AddResource(ComboPoints);
        ClassDamageTuning = BalancingDamageAura;
        GlobalCooldownSeconds = GlobalCooldown;
        GlobalCooldownScalesWithHaste = false;
        SpiritPointsPerSpiritRefund = SpiritPointsPerRefund;

        Abilities.Add(new Attack(this));
        Abilities.Add(new WindKick(this));
        Abilities.Add(new EarthFist(this));
        Abilities.Add(new SpiritPalm(this));
        Abilities.Add(new RisingStorm(this));
        Abilities.Add(new LashingStormkick(this));
        Abilities.Add(new PerfectStormStrike(this));
        Abilities.Add(new RisingEarth(this));
        Abilities.Add(new EarthfistBarrage(this));
        Abilities.Add(new DoublePalmStrike(this));
        Abilities.Add(new SpiritedVortex(this));
        Abilities.Add(new StoneShield(this));
        Abilities.Add(new ShatterEarth(this));
        Abilities.Add(new StoneStomp(this));
        Abilities.Add(new Serenity(this));
        Abilities.Add(new TwinSoulsBulwark(this));
        Abilities.Add(new TwinSoulsArmyOfOne(this));
        Abilities.Add(new Gust(this));
        Abilities.Add(new Cyclone(this));
        Abilities.Add(new Stagger(this));
        Abilities.Add(new Taunt(this));
    }

    /// <summary>
    /// A Combo Builder is added to the combo. After a third builder the combo is the last two: combat logs (27 fights) show
    /// the finisher of the last two builders after 3 or more in a row (KKF: Lashing Stormkick 49 times).
    /// </summary>
    public void AddBuilder(Technique technique, SimulationContext ctx)
    {
        if (SecondBuilder != null) FirstBuilder = SecondBuilder;
        if (FirstBuilder == null) FirstBuilder = technique;
        else SecondBuilder = technique;
        SetCombo(ctx.CurrentTime);
    }

    /// <summary>True when the combo held is <paramref name="first"/> then <paramref name="second"/>.</summary>
    public bool HasCombo(Technique first, Technique second) => FirstBuilder == first && SecondBuilder == second;

    /// <summary>A finisher spends the combo.</summary>
    public void ClearCombo(SimulationContext ctx)
    {
        FirstBuilder = null;
        SecondBuilder = null;
        SetCombo(ctx.CurrentTime);
    }

    private void SetCombo(SimTime now)
    {
        Resources.Get(ComboPoints).Current = (FirstBuilder != null ? 1 : 0) + (SecondBuilder != null ? 1 : 0);
        SetTechniqueAura(KickTechnique, Technique.Kick, now);
        SetTechniqueAura(FistTechnique, Technique.Fist, now);
        SetTechniqueAura(PalmTechnique, Technique.Palm, now);
    }

    private void SetTechniqueAura(AuraDef aura, Technique technique, SimTime now)
    {
        bool open = FirstBuilder == technique;
        if (open && !Auras.HasAura(aura, now)) Auras.Apply(aura, now);
        else if (!open && Auras.HasAura(aura, now)) Auras.Remove(aura, now);
    }

    internal void RaiseFinished(SimulationContext ctx, MeikoFinisher finisher, EnemyTarget target) => Finished?.Invoke(ctx, finisher, target);

    /// <summary>
    /// Consumes a <paramref name="charge"/> (Stormfall or Earthfall), when Meiko holds one. Hero 13's Spirit text: a Spirit
    /// Refund on the consumption gives the charge back and Rising Spirit. The refund's roll is
    /// <paramref name="refundAfterSeconds"/> after the consumption.
    /// Combat logs (16 fights): 544 of 2,232 Stormfall consumptions came back with Rising Spirit at the consumption, and 189
    /// of 937 Earthfall consumptions came back with Rising Spirit 1.0s after the Earthfist Barrage cast (its channel's end).
    /// Returns whether a charge was consumed.
    /// </summary>
    public bool ConsumeCharge(AuraDef charge, SimulationContext ctx, double refundAfterSeconds = 0.0)
    {
        var now = ctx.CurrentTime;
        if (!Auras.HasAura(charge, now)) return false;
        Auras.ConsumeStack(charge, now);
        ChargeConsumed?.Invoke(ctx, charge);
        if (refundAfterSeconds > 0.0) ctx.Schedule(SimTime.FromSeconds(refundAfterSeconds), c => RefundCharge(charge, c), "Spirit Refund");
        else RefundCharge(charge, ctx);
        return true;
    }

    private void RefundCharge(AuraDef charge, SimulationContext ctx)
    {
        if (!TriggerSpiritRefund(null, 0.0, ctx)) return;
        ctx.Schedule(SimTime.FromSeconds(ChargeRefundDelaySeconds), c => Auras.Apply(charge, c.CurrentTime), "Spirit Refund");
    }

    public override void ResetForIteration()
    {
        ResetCombatState();
        FirstBuilder = null;
        SecondBuilder = null;
    }
}
