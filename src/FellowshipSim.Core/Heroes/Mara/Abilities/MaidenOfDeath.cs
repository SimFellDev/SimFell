namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1206: increases your damage, makes all combo point generating abilities generate a full refill of
/// Combo Points, and increases your Energy Generation while Maiden of Death is active. Can be used
/// during Global Cooldown. Combat logs apply effect 2194 at every cast (1,174 casts, 45 logs in the
/// FellowshipAnalyzer Mara corpus); effect 1714 never appears.
/// </summary>
public class MaidenOfDeath : MaraAbility
{
    public const int AbilityId = 1206;
    public const string AbilityName = "Maiden of Death";
    public const double CooldownSeconds = 60.0;
    public const int BuffEffectId = 2194;
    public const double BuffSeconds = 10.0;
    public const double BuffDamage = 1.2;
    public const double EnergyRegen = 1.2;
    public const double ComboPointsPerGenerator = 6;

    /// <summary>The buff's damage is read per hit (Maiden's Doom raises it against low enemies).</summary>
    public static readonly AuraDef Buff = AuraDef.Effect("Maiden of Death", BuffEffectId, BuffSeconds);

    /// <summary>The damage bonus against an enemy (<see cref="BuffDamage"/>; Maiden's Doom raises it against low health enemies).</summary>
    public Func<EnemyTarget, double> DamageAgainst { get; set; } = _ => BuffDamage;

    public MaidenOfDeath(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        owner.EnergyRegenMultipliers.Add(now => owner.Auras.HasAura(Buff, now) ? EnergyRegen : 1.0);
        owner.TargetDamageMultipliers.Add((enemy, now) => owner.Auras.HasAura(Buff, now) ? DamageAgainst(enemy) : 1.0);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Mara.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
