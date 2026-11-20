namespace FellowshipSim.Core.Import;

/// <summary>
/// An item a hero can wear, as game data has it (<see cref="GameGearData"/>): its stats untempered at
/// <see cref="ItemLevel"/>, the most times it can be tempered, the Stamina a temper adds at its item level, the dungeons it
/// drops in and its icon URL. A legendary grants its power; a weapon its <see cref="WeaponAbilityId"/>. Item IDs are the
/// game's and are shared between heroes: each hero has its own item of an ID.
/// </summary>
public sealed record GearItem(int Id, string Name, GearKind Kind, int ItemLevel, int MaxTempers, int StaminaPerTemper, bool Legendary,
    int WeaponAbilityId, GearStats Stats, string[] Dungeons, string Icon)
{
    /// <summary>
    /// What a temper adds to each of the item's stats but Stamina (StatRows.Magnitude in game data: an item's stat is its
    /// magnitude plus its tempers, rounded; FellowshipSim.Codex.Generators' ItemGenerator checks every item at its most
    /// tempers). A temper adds <see cref="StaminaPerTemper"/> to Stamina.
    /// </summary>
    public const int StatPerTemper = 1;

    /// <summary>The item's stats with <paramref name="tempers"/> tempers (no more than <see cref="MaxTempers"/>).</summary>
    public GearStats StatsAt(int tempers)
    {
        int applied = Math.Clamp(tempers, 0, MaxTempers);
        return Stats.Raised(applied * StatPerTemper, applied * StaminaPerTemper);
    }

    /// <summary>The items <paramref name="hero"/> can wear; none for a hero game data has no gear of.</summary>
    public static IReadOnlyList<GearItem> Of(string hero) => GameGearData.Items.GetValueOrDefault(hero) ?? [];

    /// <summary><paramref name="hero"/>'s item <paramref name="id"/>, or null.</summary>
    public static GearItem? Find(string hero, int id) => Of(hero).FirstOrDefault(i => i.Id == id);
}
