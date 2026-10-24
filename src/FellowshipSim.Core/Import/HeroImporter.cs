using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.Legendaries;
using StatEnchants = FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Customization.RelicPassives;

namespace FellowshipSim.Core.Import;

/// <summary>
/// Turns a <see cref="CharacterBuild"/> into a hero ready to simulate: its stats, then its talents, relic passives, gems,
/// glyphs, weapon, augmentations and legendary, each found in the <see cref="Catalog"/> by game ID. Anything the engine
/// does not model is listed in <see cref="Hero.UnmodeledFeatures"/>.
/// </summary>
public static class HeroImporter
{
    /// <summary>The bound passive of the relic item <paramref name="itemId"/>, or null for no relic or one the catalog lacks.</summary>
    private static RelicPassive? BindRelic(Hero hero, int itemId)
    {
        if (itemId == 0) return null;
        var relicPassive = Catalog.RelicPassiveOfItem(itemId);
        if (relicPassive == null)
        {
            hero.UnmodeledFeatures.Add($"Relic item {itemId}");
            return null;
        }
        relicPassive.Bind(hero);
        return relicPassive;
    }

    public static Hero Import(CharacterBuild build)
    {
        var hero = Catalog.CreateHero(build.Hero);
        hero.Name = build.Name;

        // Base stats: ratings from gear, and the character sheet's percentages when the build comes from a log
        // (they already include passive bonuses such as gem nodes).
        hero.Stats.PrimaryStat = build.PrimaryStat;
        hero.Stats.Stamina = build.Stamina;
        if (build.MaxHealth > 0) hero.Stats.LoggedMaxHealth = build.MaxHealth;
        hero.Stats.Armor = build.Armor;
        // Stat enchants add their rating to gear's; a character sheet already counts them.
        bool sheet = build.SheetPercents.Count > 0;
        hero.Stats.CritRating = build.CritRating + (sheet ? 0 : build.EnchantRating(StatType.CritRating));
        hero.Stats.HasteRating = build.HasteRating + (sheet ? 0 : build.EnchantRating(StatType.HasteRating));
        hero.Stats.ExpertiseRating = build.ExpertiseRating + (sheet ? 0 : build.EnchantRating(StatType.ExpertiseRating));
        hero.Stats.SpiritRating = build.SpiritRating + (sheet ? 0 : build.EnchantRating(StatType.SpiritRating));
        if (sheet)
        {
            hero.Stats.SheetCritChance = build.SheetPercents.GetValueOrDefault("crit");
            hero.Stats.SheetHaste = build.SheetPercents.GetValueOrDefault("haste");
            hero.Stats.SheetExpertise = build.SheetPercents.GetValueOrDefault("expertise");
            hero.Stats.SheetSpirit = build.SheetPercents.GetValueOrDefault("spirit");
        }

        hero.Initialize();
        foreach (var ability in hero.Abilities.ToList()) ability.Connect();

        foreach (int talentId in build.Talents)
        {
            var talent = Catalog.CreateTalent(talentId);
            if (talent == null)
            {
                hero.UnmodeledFeatures.Add($"Talent {talentId}");
                continue;
            }
            hero.Talents.Add(talent);
            talent.Bind(hero);
        }

        foreach (var entry in build.StatEnchants.Where(e => StatEnchants.StatEnchant.Find(e.Id) == null))
        {
            hero.UnmodeledFeatures.Add($"Stat enchant {entry.Name ?? entry.Id.ToString()}");
        }

        // The relic passives before gems: Covetous Greed raises every gem colour's power (already in a sheet's gem power).
        hero.Relic1 = BindRelic(hero, build.Relic1ItemId);
        hero.Relic2 = BindRelic(hero, build.Relic2ItemId);

        foreach (var (color, power) in build.GemPower)
        {
            var gem = Gem.Create(color);
            gem.Power = hero.Stats.UsesSheetValues ? power : power * hero.RelicPassives.Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
            hero.Gems.Add(gem);
            gem.Bind(hero);
            hero.UnmodeledFeatures.AddRange(gem.UnmodeledNodes.Select(n => $"Gem node: {n}"));
        }

        foreach (var entry in build.Glyphs)
        {
            var glyph = Catalog.CreateGlyph(entry.Id, entry.Rank);
            if (glyph == null)
            {
                hero.UnmodeledFeatures.Add($"Glyph: {entry.Name ?? entry.Id.ToString()} (rank {entry.Rank})");
                continue;
            }
            hero.Glyphs.Add(glyph);
            glyph.Bind(hero);
        }

        // Weapon before augmentations: some augmentations key off the weapon ability.
        if (build.WeaponAbilityId != 0)
        {
            var weapon = Catalog.CreateWeapon(build.WeaponAbilityId);
            if (weapon == null) hero.UnmodeledFeatures.Add($"Weapon with ability {build.WeaponAbilityId}");
            weapon?.Equip(hero);
        }

        foreach (var entry in build.Augmentations)
        {
            var augmentation = Catalog.CreateAugmentation(entry.Id, entry.Rank);
            if (augmentation == null)
            {
                hero.UnmodeledFeatures.Add($"Augmentation: {entry.Name ?? entry.Id.ToString()} (rank {entry.Rank})");
                continue;
            }
            hero.Augmentations.Add(augmentation);
            augmentation.Bind(hero);
        }

        if (build.LegendaryItemIds.Count > 0)
        {
            foreach (int itemId in build.LegendaryItemIds)
            {
                var legendary = Catalog.CreateLegendary(build.Hero, itemId);
                if (legendary == null || !legendary.IsModeled) hero.UnmodeledFeatures.Add($"Legendary item {itemId}");
                if (legendary == null) continue;
                hero.Legendaries.Add(legendary);
                legendary.Bind(hero);
            }
            hero.PassiveAuras.Add(LegendaryStats.StrandOfEternity);
            if (!hero.Stats.UsesSheetValues) hero.PassiveAuras.Add(LegendaryStats.MoteOfCreation);
        }

        return hero;
    }
}
