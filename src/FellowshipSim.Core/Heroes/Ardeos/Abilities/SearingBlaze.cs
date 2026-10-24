namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>Ability 1587: instant, applies Searing Blaze (<see cref="SearingBlazeDot"/>) to the target.</summary>
public class SearingBlaze : ArdeosAbility
{
    public const int AbilityId = 1587;
    public const string AbilityName = "Searing Blaze";

    public SearingBlazeDot Dot { get; }

    public SearingBlaze(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        Dot = new SearingBlazeDot(owner);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        if (target != null) Ardeos.Dots.Apply(ctx, Dot, target);
    }
}
