namespace FellowshipSim.Core.Customization.Gems;

/// <summary>
/// A gem that goes in a gear socket (game data "gem" records): its colour and the gem power it gives. A hero has
/// <see cref="Sockets"/> sockets; which items they are on does not matter. Gear also rolls bonus gem power of a colour,
/// <see cref="GearBonusPower"/> per bonus (combat logs: every <c>gemPowers</c> entry on an item equals it; per the user).
/// A colour's gem power is its gems' and its gear bonuses' power, times the relics' gem power multiplier (combat logs:
/// Cf7npHNzRmALFJQy fight 1, Amethyst with Covetous Greed).
/// </summary>
public sealed record GemItem(int Id, string Name, GemColor Color, int Power)
{
    /// <summary>Gem sockets a hero has across their gear (per the user).</summary>
    public const int Sockets = 4;

    /// <summary>Gem power of one bonus on a piece of gear.</summary>
    public const int GearBonusPower = 100;

    public const int SmallPower = 150;
    public const int LargePower = 250;
    public const int SplendidPower = 350;
    public const int FlawlessPower = 500;

    /// <summary>Every gem, by size then colour (game data "gem" records 22 to 51).</summary>
    public static IReadOnlyList<GemItem> All { get; } =
    [
        new(22, "Small Amethyst", GemColor.Amethyst, SmallPower),
        new(27, "Small Diamond", GemColor.Diamond, SmallPower),
        new(32, "Small Emerald", GemColor.Emerald, SmallPower),
        new(38, "Small Ruby", GemColor.Ruby, SmallPower),
        new(43, "Small Sapphire", GemColor.Sapphire, SmallPower),
        new(48, "Small Topaz", GemColor.Topaz, SmallPower),
        new(23, "Large Amethyst", GemColor.Amethyst, LargePower),
        new(28, "Large Diamond", GemColor.Diamond, LargePower),
        new(33, "Large Emerald", GemColor.Emerald, LargePower),
        new(39, "Large Ruby", GemColor.Ruby, LargePower),
        new(44, "Large Sapphire", GemColor.Sapphire, LargePower),
        new(49, "Large Topaz", GemColor.Topaz, LargePower),
        new(24, "Splendid Amethyst", GemColor.Amethyst, SplendidPower),
        new(29, "Splendid Diamond", GemColor.Diamond, SplendidPower),
        new(34, "Splendid Emerald", GemColor.Emerald, SplendidPower),
        new(40, "Splendid Ruby", GemColor.Ruby, SplendidPower),
        new(45, "Splendid Sapphire", GemColor.Sapphire, SplendidPower),
        new(50, "Splendid Topaz", GemColor.Topaz, SplendidPower),
        new(25, "Flawless Amethyst", GemColor.Amethyst, FlawlessPower),
        new(30, "Flawless Diamond", GemColor.Diamond, FlawlessPower),
        new(35, "Flawless Emerald", GemColor.Emerald, FlawlessPower),
        new(41, "Flawless Ruby", GemColor.Ruby, FlawlessPower),
        new(46, "Flawless Sapphire", GemColor.Sapphire, FlawlessPower),
        new(51, "Flawless Topaz", GemColor.Topaz, FlawlessPower),
    ];

    /// <summary>The gem with game ID <paramref name="id"/>, or null.</summary>
    public static GemItem? Find(int id) => All.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// Each colour's gem power from <paramref name="socketed"/> gem IDs and <paramref name="gearBonuses"/> (bonuses per
    /// colour), times <paramref name="multiplier"/> (the relics' gem power multiplier); colours with none are left out.
    /// </summary>
    public static Dictionary<GemColor, double> PowerByColor(IEnumerable<int> socketed, IReadOnlyDictionary<GemColor, int> gearBonuses, double multiplier)
    {
        var power = new Dictionary<GemColor, double>();
        foreach (var gem in socketed.Select(Find).OfType<GemItem>()) power[gem.Color] = power.GetValueOrDefault(gem.Color) + gem.Power;
        foreach (var (color, count) in gearBonuses) power[color] = power.GetValueOrDefault(color) + count * GearBonusPower;
        foreach (var color in power.Keys.ToList())
        {
            if (power[color] <= 0) power.Remove(color);
            else power[color] = Math.Floor(power[color] * multiplier);
        }
        return power;
    }
}
