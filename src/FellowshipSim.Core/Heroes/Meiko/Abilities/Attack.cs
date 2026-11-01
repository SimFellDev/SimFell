namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1118: Meiko's auto attack, 46% of Agility (game data coefficient 0.46), a swing every 1.9 seconds
/// (Meiko.MeleeAutoAttack.SwingTimer 1.9) shortened by Haste. Combat logs record every swing as a cast (7Zmfx9gq6HjvThzc
/// fight 2: swings 1.37-1.47s apart). Each swing adds a stack to Serenity.
/// </summary>
public class Attack : MeikoAbility
{
    public const int AbilityId = 1118;
    public const string AbilityName = "Attack";
    public const double Coefficient = 0.46;
    public const double SwingSeconds = 1.9;

    public Attack(Meiko owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(SwingSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is not { IsSleeping: false }) return;
        DealDamage(ctx, target);
        // "Each time you Auto Attack an enemy, your Serenity gains +40% increased healing, stacking up to 50 times."
        Owner.Auras.Apply(Serenity.Stacks, ctx.CurrentTime);
    }
}
