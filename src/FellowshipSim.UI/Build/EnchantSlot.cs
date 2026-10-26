using FellowshipSim.Core.Customization.ProtectiveEnchants;

namespace FellowshipSim.UI.Build;

/// <summary>
/// The kinds of enchant a build's gear holds, each counted against its own limit and all but the protective enchants
/// against <see cref="EnchantSlots.Total"/> (per the user). Each item carries one aspect, sigil, glyph or stat enchant, the neck the protective
/// enchants, and each +100 gem power bonus is one more. Combat logs: every non-legendary item holds exactly 3 slots (its
/// gem power bonuses, aspect, sigil and glyph ranks, and a stat enchant's rating over 6 rounded up), so full gear fills
/// the 44 with a weapon (40 of 46 players of patch 0.5.0.0).
/// </summary>
public enum EnchantSlot { StatEnchants, Gems, Aspects, Sigils, Glyphs, Protective }

/// <summary>The limits of each <see cref="EnchantSlot"/> (per the user).</summary>
public static class EnchantSlots
{
    /// <summary>The armor pieces that take aspects, sigils and glyphs as well (head, shoulders, chest, back, hands, waist and feet, less the legendary's).</summary>
    public const int ArmorPieces = 6;

    /// <summary>The pieces that take stat enchants and gem power bonuses alone (the weapon item, neck, wrists, rings and trinkets).</summary>
    public const int OtherPieces = 7;

    /// <summary>Enchants each piece holds.</summary>
    public const int SlotsPerPiece = 3;

    /// <summary>The most enchants of one kind a piece holds (combat logs: no item of 89 players holds more).</summary>
    public const int MostOfAKindPerPiece = 2;

    /// <summary>Enchants a build wears in all, without the weapon's; protective enchants are not counted.</summary>
    public const int Total = SlotsPerPiece * (ArmorPieces + OtherPieces);

    /// <summary>What a weapon adds to the limits: 2 sigils, 2 stat enchants and 1 aspect (5 in all).</summary>
    public const int WeaponSigils = 2;
    public const int WeaponStatEnchants = 2;
    public const int WeaponAspects = 1;

    /// <summary>Rating one stat enchant adds to each of its stats (combat logs: 775 of 842 at the current item level).</summary>
    public const double RatingPerStatEnchant = 6;

    /// <summary>The limit of <paramref name="slot"/>, with a weapon's slots when <paramref name="weapon"/>.</summary>
    public static int Limit(EnchantSlot slot, bool weapon) => slot switch
    {
        EnchantSlot.StatEnchants => 26 + (weapon ? WeaponStatEnchants : 0),
        EnchantSlot.Gems => 26,
        EnchantSlot.Aspects => 12 + (weapon ? WeaponAspects : 0),
        EnchantSlot.Sigils => 12 + (weapon ? WeaponSigils : 0),
        EnchantSlot.Glyphs => 12,
        _ => ProtectiveEnchant.MaxPerBuild,
    };

    /// <summary>Enchants worn in all, with a weapon's slots when <paramref name="weapon"/>.</summary>
    public static int TotalLimit(bool weapon) => Total + (weapon ? WeaponSigils + WeaponStatEnchants + WeaponAspects : 0);

    public static string Name(EnchantSlot slot) => slot switch
    {
        EnchantSlot.StatEnchants => "stat enchants",
        EnchantSlot.Gems => "gem power bonuses",
        EnchantSlot.Aspects => "aspect ranks",
        EnchantSlot.Sigils => "sigil ranks",
        EnchantSlot.Glyphs => "glyph ranks",
        _ => "protective enchants",
    };
}
