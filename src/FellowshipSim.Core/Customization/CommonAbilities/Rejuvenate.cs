namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 189 (a relic ability): heals the caster for <see cref="MaxHealthFraction"/> of their maximum health, with no spread.
/// Instant, with no global cooldown, and a 120s cooldown (row `RelicPower.HealthPotion.Cooldown`) times the relic cooldown gem
/// factor. Game data (build 26320967). The engine has no health of the hero's,
/// so the heal changes nothing. No damage.
/// </summary>
public class Rejuvenate : RelicPower
{
    public const int AbilityId = 189;
    public const string AbilityName = "Rejuvenate";
    public const double CooldownSeconds = 120.0;

    /// <summary>Game data: row `RelicPower.HealthPotion.MaxHealthFraction`.</summary>
    public const double MaxHealthFraction = 0.4;

    public Rejuvenate(Hero owner) : base(owner, AbilityId, AbilityName, CooldownSeconds)
    {
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) => TriggerCooldown(ctx);
}
