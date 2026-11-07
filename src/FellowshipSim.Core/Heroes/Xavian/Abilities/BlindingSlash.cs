namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1858: "Your sword is infused with the light of sun power as you slash at target enemy, dealing physical damage
/// to them and other enemies near it. You apply 1 stack of Blind to enemies hit by Blinding Slash for up to 8 seconds,
/// causing their Auto Attacks to have an additional +20% chance to be Parried by you. ... Blinding Slash has a 15% chance
/// to grant you 1 stack of Swift Reprieval."
/// </summary>
public class BlindingSlash : XavianAbility
{
    public const int AbilityId = 1858;
    public const string AbilityName = "Blinding Slash";
    /// <summary>Placeholder BlindSlashDmg: 123% of Strength.</summary>
    public const double Coefficient = 1.23;
    /// <summary>Game data cooldown 6, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 6.0;
    /// <summary>Game data targetCountFalloff: effect 2568 (Blinding Slash), threshold 8.</summary>
    public const double TargetThreshold = 8;

    /// <summary>Game data generates Resources 1, chance 0.15 (GA_Sune_InstantCleaveMelee @1311).</summary>
    public const double SwiftReprievalChance = 0.15;
    public const string ProcKey = "blinding_slash_swift_reprieval";

    /// <summary>Effect 2555 "Blinded": 8 seconds, up to 2 stacks, +20% chance to be Parried.</summary>
    public const int BlindedEffectId = 2555;
    public const double BlindedSeconds = 8.0;
    public const int BlindedMaxStacks = 2;
    public static readonly AuraDef Blinded = AuraDef.Effect("Blinded", BlindedEffectId, BlindedSeconds, BlindedMaxStacks);

    public BlindingSlash(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var enemies = Area(ctx, target);
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        DealToEach(ctx, struck, TargetThreshold);
        for (int i = 0; i < struck.Count; i++) struck[i].Auras.Apply(Blinded, ctx.CurrentTime);
        if (ctx.Outcomes.RollProc(ProcKey, SwiftReprievalChance, ctx)) Xavian.GrantSwiftReprieval(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
