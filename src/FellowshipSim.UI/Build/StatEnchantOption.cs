using FellowshipSim.Core.Customization.StatEnchants;

namespace FellowshipSim.UI.Build;

/// <summary>A stat enchant as the build editor shows it: the enchant, its name for the hero's main stat, and its two stats.</summary>
public sealed record StatEnchantOption(StatEnchant Enchant, string Name)
{
    /// <summary>"Crit + Expertise".</summary>
    public string StatsText => $"{StatName(Enchant.FirstRating)} + {StatName(Enchant.SecondRating)}";

    private static string StatName(FellowshipSim.Core.Engine.StatType rating) => rating.ToString().Replace("Rating", "");
}
