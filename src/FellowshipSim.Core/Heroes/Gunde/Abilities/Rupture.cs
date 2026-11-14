namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2293: "Strike target enemy by driving both axes upwards through their torso and splitting their anatomy open,
/// dealing damage and applying Open Wounds. Rupture applies Rend to the target. Open Wounds: Your next Slaughter
/// consumes Open Wounds, increasing Slaughter's damage against that enemy." The cooldown is not hasted.
/// </summary>
public partial class Rupture : GundeAbility
{
    public const int OpenWoundsEffectId = 3233;
    public const double OpenWoundsSeconds = 18.0;
    public const double OpenWoundsSlaughterDamage = 1.2;

    /// <summary>Open Wounds on the enemy: Slaughter deals more to it, and uses it up.</summary>
    public static readonly AuraDef OpenWounds = AuraDef.Effect("Open Wounds", OpenWoundsEffectId, OpenWoundsSeconds);

    public Rupture(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown);
        RendShare = Constants.PerTargetAccumulatedBleedDamageToDotScaler;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        RendingPulse(ctx, BeginRend(ctx), Single(target));
        if (target is { IsSleeping: false }) target.Auras.Apply(OpenWounds, ctx.CurrentTime);
    }
}
