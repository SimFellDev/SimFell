namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Base for Meiko's finishers (ability 1099, Fighting Techniques): each needs its own combo of two builders and spends it.
/// "Finisher Techniques can be used during Global Cooldown." Combat logs (27 fights, 5,309 finishers after 2 builders):
/// the first builder picks the technique and the second the finisher. Kick then Fist is Lashing Stormkick (2,095), Kick
/// then Palm Rising Storm (856), Fist then Kick Earthfist Barrage (898), Fist then Palm Rising Earth (397), Palm then Kick
/// Double Palm Strike (552), Palm then Fist Spirited Vortex (511).
/// </summary>
public abstract class MeikoFinisher : MeikoAbility
{
    protected MeikoFinisher(Meiko owner, int id, string name, AbilityCategory category, Technique first, Technique second)
        : base(owner, id, name, category)
    {
        First = first;
        Second = second;
        IgnoresGcd = true;
    }

    /// <summary>The combo's first builder: the finisher's technique.</summary>
    public Technique First { get; }

    /// <summary>The combo's second builder.</summary>
    public Technique Second { get; }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (!Meiko.HasCombo(First, Second))
        {
            reason = explain ? $"{Name} needs the combo {First} then {Second}" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Meiko.ClearCombo(ctx);
        Strike(ctx, target);
        Meiko.RaiseFinished(ctx, this, target);
    }

    /// <summary>What the finisher does.</summary>
    protected abstract void Strike(SimulationContext ctx, EnemyTarget target);
}
