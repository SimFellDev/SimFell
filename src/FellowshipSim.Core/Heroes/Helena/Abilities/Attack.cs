namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 978: Helena's weapon swing, 40% of Strength (game data coefficient 0.4). Combat logs record every swing as a
/// cast; an action list uses it as soon as the swing timer allows. UNVERIFIED: the swing is taken to be 1.5s shortened by
/// Haste, as Gunde's.
/// </summary>
public class Attack : HelenaAbility
{
    public const int AbilityId = 978;
    public const string AbilityName = "Attack";
    public const double Coefficient = 0.4;
    public const double SwingSeconds = 1.5;

    public Attack(Helena owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(SwingSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is { IsSleeping: false }) DealDamage(ctx, target);
    }
}
