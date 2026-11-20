namespace FellowshipSim.Core.Engine;

/// <summary>
/// One enemy of a dungeon fight, for the party's damage: its health falls from <see cref="MaxHealth"/> at
/// <see cref="Start"/> to none at <see cref="DiesAt"/>, and <see cref="HeroDamage"/> is the hero's damage to it.
/// <see cref="BossFight"/> is true for an enemy of a pull with a boss in it, or of a pull entering combat while a boss is
/// in combat.
/// </summary>
internal readonly record struct PartyEnemy(SimTime Start, SimTime DiesAt, double MaxHealth, double HeroDamage, bool BossFight)
{
    /// <summary>The health the enemy lost from its start to <paramref name="time"/>.</summary>
    public double HealthLost(SimTime time)
    {
        double seconds = (DiesAt - Start).TotalSeconds;
        if (seconds <= 0) return time >= DiesAt ? MaxHealth : 0;
        return MaxHealth * Math.Clamp((time - Start).TotalSeconds / seconds, 0, 1);
    }
}
