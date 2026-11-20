using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Import;

namespace FellowshipSim.UI.Build;

/// <summary>A stat enchant as the build editor shows it: the enchant, its name for the hero's main stat, and its two stats.</summary>
public sealed record StatEnchantOption(StatEnchant Enchant, string Name)
{
    /// <summary>The icon's image on the game data CDN, or null when the game data has none.</summary>
    public string? IconUrl => GameIcons.StatEnchants.GetValueOrDefault(Enchant.Id);

    /// <summary>"Crit + Expertise".</summary>
    public string StatsText => $"{StatName(Enchant.FirstRating)} + {StatName(Enchant.SecondRating)}";

    private static string StatName(FellowshipSim.Core.Engine.StatType rating) => rating.ToString().Replace("Rating", "");
}
