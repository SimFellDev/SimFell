namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1206: "For the next 10 seconds, you deal 20% more damage and all combo point generating abilities generate 6
/// Combo Points. Additionally, your Energy Generation is increased by 20% while Maiden of Death is active. Can be used
/// during Global Cooldown." 60s cooldown. Combat logs apply effect 2194 at every cast (1,174 casts, 45 logs in the
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

    /// <summary>The damage bonus against an enemy (1.2; Maiden's Doom: 1.4 at 30% health or less).</summary>
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
