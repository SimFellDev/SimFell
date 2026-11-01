namespace FellowshipSim.Core.Heroes.Helena.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>
/// Base for Helena's abilities. An ability with a Veteran of War line lists its targets and seconds in
/// <see cref="VeteranOfWarReductions"/>, and calls <see cref="ApplyVeteranOfWar"/> as its cast resolves.
/// </summary>
public abstract class HelenaAbility : Ability
{
    protected HelenaAbility(Helena owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Helena Helena => (Helena)Owner;

    /// <summary>The abilities this one's Veteran of War line names, each with its seconds off.</summary>
    public List<(Type Target, double Seconds)> VeteranOfWarReductions { get; } = new();

    protected void ApplyVeteranOfWar(SimulationContext ctx) => Helena.VeteranOfWar(ctx, this);

    /// <summary>Every enemy in combat, or <paramref name="target"/> alone when the fight lists none.</summary>
    protected static IReadOnlyList<EnemyTarget> Enemies(SimulationContext ctx, EnemyTarget target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
