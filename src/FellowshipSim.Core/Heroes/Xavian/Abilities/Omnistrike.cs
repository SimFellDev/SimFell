namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1863: "Swing your sword in a large arc around you, dealing physical damage to all enemies around you. Generates
/// 4 Spirit Points. Omnistrike has a 30% chance to grant you 1 stack of Swift Reprieval." Sunstruck: "your next
/// Omnistrike consumes all stacks of Sunstruck on all targets hit, replenishing 100 Mana plus an additional 50 Mana for
/// each stack beyond the first that is consumed."
/// </summary>
public partial class Omnistrike : XavianAbility
{
    public const string ProcKey = "omnistrike_swift_reprieval";

    /// <summary>
    /// Sune.AoeMeleeDamageBoostAndManaRefundDebuff.ManaRefundBase 100 and ManaRefundPerAdditionalStack 50 (ability 1845's
    /// text has 50; effect 2664's text has 40). UNVERIFIED.
    /// </summary>
    public const double SunstruckManaBase = 100;
    public const double SunstruckManaPerAdditionalStack = 50;

    /// <summary>Damage bonus per Sunstruck stack consumed, on every enemy hit (Gleaming Strikes: 0.25).</summary>
    public double DamagePerSunstruckStack { get; set; }

    /// <summary>Raised after the cast's hits (Rising Sun, Golden Hour's Omega Reprieval, Light's Oath).</summary>
    public event Action<SimulationContext>? Struck;

    public Omnistrike(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        var enemies = Area(ctx, target);
        if (enemies.Count > AbilityMaxTargets) enemies = enemies.Take(AbilityMaxTargets).ToList();
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);

        int consumed = 0;
        for (int i = 0; i < struck.Count; i++) consumed += struck[i].Auras.GetStacks(SunStrike.Sunstruck, now);
        double multiplier = 1.0 + DamagePerSunstruckStack * consumed;
        DealToEach(ctx, struck, Constants.DamageScalingTargetCountThreshold, multiplier);

        if (consumed > 0)
        {
            for (int i = 0; i < struck.Count; i++) struck[i].Auras.Remove(SunStrike.Sunstruck, now);
            Xavian.GainMana(SunstruckManaBase + SunstruckManaPerAdditionalStack * (consumed - 1), ctx, target);
        }
        Owner.GainResource(Hero.SpiritPoints, Constants.SpiritPointGain, ctx);
        if (ctx.Outcomes.RollProc(ProcKey, Constants.ResourceProcChance, ctx)) Xavian.GrantSwiftReprieval(ctx);
        Struck?.Invoke(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
