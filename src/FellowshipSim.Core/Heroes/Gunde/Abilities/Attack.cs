namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 1249: Gunde's weapon swing, a share of Strength (game data coefficient <see cref="Coefficient"/>). Combat logs
/// record every swing as a cast; an action list uses it as soon as the swing timer allows. Combat logs
/// (n7zxK8harZT2wjDB fight 9): the swing interval is the base swing time shortened by Haste. Applies no Rend.
/// </summary>
public class Attack : GundeAbility
{
    public const int AbilityId = 1249;
    public const string AbilityName = "Attack";
    public const double Coefficient = 0.3;
    public const double SwingSeconds = 1.5;

    public Attack(Gunde owner) : base(owner, AbilityId, AbilityName)
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
