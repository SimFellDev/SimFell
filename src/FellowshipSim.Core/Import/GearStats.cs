namespace FellowshipSim.Core.Import;

/// <summary>The stats an item lists: Stamina, the hero's primary stat and the four ratings.</summary>
public readonly record struct GearStats(int Stamina, int Primary, int Crit, int Expertise, int Haste, int Spirit)
{
    /// <summary>
    /// These stats with <paramref name="stamina"/> added to Stamina and <paramref name="amount"/> to each other stat the item
    /// has (a stat it has none of stays 0).
    /// </summary>
    public GearStats Raised(int amount, int stamina)
    {
        static int Up(int stat, int by) => stat == 0 ? 0 : stat + by;
        return new(Up(Stamina, stamina), Up(Primary, amount), Up(Crit, amount), Up(Expertise, amount), Up(Haste, amount), Up(Spirit, amount));
    }

    public static GearStats operator +(GearStats a, GearStats b) => new(a.Stamina + b.Stamina, a.Primary + b.Primary,
        a.Crit + b.Crit, a.Expertise + b.Expertise, a.Haste + b.Haste, a.Spirit + b.Spirit);
}
