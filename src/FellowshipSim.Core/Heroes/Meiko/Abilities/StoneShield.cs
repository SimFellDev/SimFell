namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1122: "Summon 3 protective stones around you that spin in a wide circle, dealing 135.2% of Agility physical
/// damage 4 times to all nearby enemies. ... Stone Shield gains Cooldown Acceleration equal to your Haste. Can be used
/// during Global Cooldown". 30 second cooldown, 2 charges. The stones' damage is ability 1121, logged as effect 1572.
/// </summary>
public partial class StoneShield : MeikoAbility
{
    public const int DamageEffectId = 1572;
    /// <summary>
    /// Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 20.803 and 31.656): each enemy takes 5 hits, 0.04, 0.27, 0.50, 0.77 and 1.00
    /// seconds after the cast, though the tooltip says 4.
    /// </summary>
    public const int Hits = 5;

    public StoneShield(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageAgilityCoefficient;
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
        IgnoresGcd = true;
        AoeTargetThreshold = Constants.TargetCountThreshold;
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        PeriodicEffect.Begin(ctx, Name, (Hits - 1) * Constants.DamageTickInterval, Constants.DamageTickInterval, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) => DealAoeDamage(c, c.ActiveEnemies),
            tickDelaySeconds: PeriodicEffect.ServerFrameSeconds, damageLogId: DamageLogId);
    }
}
