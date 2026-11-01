namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1122: "Summon 3 protective stones around you that spin in a wide circle, dealing 135.2% of Agility physical
/// damage 4 times to all nearby enemies. Your armor is increased by 20% for every 1 stone that is active on you, up to a
/// maximum of 60% increased armor with 3 stones. While at least one stone is active, they soak 35% of all damage you take.
/// ... Stone Shield gains Cooldown Acceleration equal to your Haste. Can be used during Global Cooldown". 30 second
/// cooldown, 2 charges. The stones' damage is ability 1121, logged as effect 1572. Armor and the soak are not simulated:
/// the stones stay until the next cast.
/// </summary>
public class StoneShield : MeikoAbility
{
    public const int AbilityId = 1122;
    public const string AbilityName = "Stone Shield";
    /// <summary>Placeholder StoneShieldDmg: 135.2% of Agility.</summary>
    public const double Coefficient = 1.352;
    public const double CooldownSeconds = 30.0;
    public const int Charges = 2;
    public const int DamageEffectId = 1572;
    /// <summary>Game data targetCountFalloff: effect 1572, threshold 5.</summary>
    public const double TargetThreshold = 5;
    /// <summary>
    /// Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 20.803 and 31.656): each enemy takes 5 hits, 0.04, 0.27, 0.50, 0.77 and 1.00
    /// seconds after the cast, though the tooltip says 4.
    /// </summary>
    public const int Hits = 5;
    public const double HitPeriodSeconds = 0.25;
    /// <summary>Effect 1574 (maxStacks 3, infinite): the stones. Combat logs: every cast applies it at 3 stacks.</summary>
    public const int StonesEffectId = 1574;
    public const int MaxStones = 3;

    public static readonly AuraDef Stones = AuraDef.Effect("Stone Shield", StonesEffectId, maxStacks: MaxStones);

    public StoneShield(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Defensive)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
        IgnoresGcd = true;
        AoeTargetThreshold = TargetThreshold;
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        Owner.Auras.Remove(Stones, now);
        Owner.Auras.Apply(Stones, now, MaxStones);
        PeriodicEffect.Begin(ctx, Name, (Hits - 1) * HitPeriodSeconds, HitPeriodSeconds, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) => DealAoeDamage(c, c.ActiveEnemies),
            tickDelaySeconds: PeriodicEffect.ServerFrameSeconds, damageLogId: DamageLogId);
    }
}
