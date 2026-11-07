namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1586: a cast with charges (cooldown recovery, not haste) that applies an instance of Engulfing Flames
/// (<see cref="EngulfingFlamesDot"/>). REKINDLED FLAME: each stack of Engulfing Flames active on an enemy as it dies
/// reduces the cooldown of Engulfing Flames.
/// </summary>
public class EngulfingFlames : ArdeosAbility
{
    public const int AbilityId = 1586;
    public const string AbilityName = "Engulfing Flames";
    public const double CastTimeSeconds = 1.5;
    public const double CooldownSeconds = 20.0;
    public const int Charges = 2;
    public const double RekindledFlameSeconds = 10.0;

    public EngulfingFlamesDot Dot { get; }

    public EngulfingFlames(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        SetCooldown(CooldownSeconds, Charges);
        Dot = new EngulfingFlamesDot(owner);
        owner.Dots.EnemyDying += (ctx, _, dots) =>
        {
            int instances = dots.Count(d => d.Effect == Dot);
            if (instances > 0) Owner.ReduceCooldown(Name, SimTime.FromSeconds(RekindledFlameSeconds * instances), ctx.CurrentTime);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target != null) Ardeos.Dots.Apply(ctx, Dot, target);
    }
}
