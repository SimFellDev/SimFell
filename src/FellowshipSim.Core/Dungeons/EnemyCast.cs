namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// An enemy's interruptible cast: <see cref="Ability"/>, begun every <see cref="CooldownSeconds"/> from one cooldown after
/// the enemy arrives, <see cref="CastSeconds"/> long. A cast the hero does not interrupt completes with no effect.
/// </summary>
public sealed record EnemyCast(string Ability, double CooldownSeconds, double CastSeconds = EnemyCast.DefaultCastSeconds)
{
    /// <summary>The cast time of a cast whose text names none.</summary>
    public const double DefaultCastSeconds = 2.5;

    /// <summary>The problem with the cast's values, or null when they are valid.</summary>
    public string? Problem() => this switch
    {
        _ when string.IsNullOrWhiteSpace(Ability) => "the interrupt ability has a name",
        _ when !(CooldownSeconds > 0) => "the cooldown is more than 0 seconds",
        _ when !(CastSeconds > 0) || CastSeconds > CooldownSeconds => "the cast time is more than 0 seconds and at most the cooldown",
        _ => null,
    };
}
