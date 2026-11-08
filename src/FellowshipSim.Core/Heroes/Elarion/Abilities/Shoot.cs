namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1303: Elarion's bow shot between abilities, a share of Agility (game data <see cref="AgilityCoefficient"/>). Combat logs record
/// every shot as a cast; an action list uses it as soon as the shot timer allows, and never while casting or channeling.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): shots are spaced by a timer that Haste shortens.
/// UNVERIFIED: <see cref="ShotSeconds"/> as the timer.
/// </summary>
public partial class Shoot : ElarionAbility
{
    public const double ShotSeconds = 1.5;

    public Shoot(Elarion owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = AgilityCoefficient;
        SetCooldown(ShotSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Owner.CurrentCast != null || Owner.CurrentChannel != null)
        {
            reason = explain ? $"{Name} waits for the cast or channel to end" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false }) DealDamage(ctx, target);
    }
}
