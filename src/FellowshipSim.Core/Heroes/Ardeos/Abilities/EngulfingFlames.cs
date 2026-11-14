namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1586: a cast with charges (cooldown recovery, not haste) that applies an instance of Engulfing Flames
/// (<see cref="EngulfingFlamesDot"/>). REKINDLED FLAME: each stack of Engulfing Flames active on an enemy as it dies
/// reduces the cooldown of Engulfing Flames.
/// </summary>
public partial class EngulfingFlames : ArdeosAbility
{
    public EngulfingFlamesDot Dot { get; }

    public EngulfingFlames(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        SetCooldown(AbilityCooldown, AbilityCharges);
        Dot = new EngulfingFlamesDot(owner);
        owner.Dots.EnemyDying += (ctx, _, dots) =>
        {
            int instances = dots.Count(d => d.Effect == Dot);
            if (instances > 0) Owner.ReduceCooldown(Name, SimTime.FromSeconds(Constants.CooldownReductionInSecondsOnTargetDeath * instances), ctx.CurrentTime);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target != null) Ardeos.Dots.Apply(ctx, Dot, target);
    }
}
