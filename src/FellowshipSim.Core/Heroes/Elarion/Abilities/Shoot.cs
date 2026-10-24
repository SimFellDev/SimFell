namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1303: Elarion's bow shot between abilities, 60% of Agility (game data coefficient 0.6). Combat logs record
/// every shot as a cast; an action list uses it as soon as the shot timer allows, and never while casting or channeling.
/// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): shots at least 1.1s apart, most 1.3-1.8s.
/// UNVERIFIED: the 1.5s timer shortened by Haste.
/// </summary>
public class Shoot : ElarionAbility
{
    public const int AbilityId = 1303;
    public const string AbilityName = "Shoot";
    public const double Coefficient = 0.6;
    public const double ShotSeconds = 1.5;

    public Shoot(Elarion owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
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
