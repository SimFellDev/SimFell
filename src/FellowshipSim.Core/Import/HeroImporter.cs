using FellowshipSim.Core.Customization.CommonAbilities;
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

        // Base stats: the gear's, with the hero's base Main Stat and Stamina and the stat enchants' ratings; every other
        // bonus adds to them as it binds.
        hero.Stats.PrimaryStat = build.PrimaryStat + StatBlock.BasePrimaryStat;
        hero.Stats.Stamina = build.Stamina + StatBlock.BaseStamina;
        if (build.MaxHealth > 0) hero.Stats.LoggedMaxHealth = build.MaxHealth;
        hero.Stats.Armor = build.Armor;
        hero.Stats.CritRating = build.CritRating + build.EnchantRating(StatType.CritRating);
        hero.Stats.HasteRating = build.HasteRating + build.EnchantRating(StatType.HasteRating);
        hero.Stats.ExpertiseRating = build.ExpertiseRating + build.EnchantRating(StatType.ExpertiseRating);
        hero.Stats.SpiritRating = build.SpiritRating + build.EnchantRating(StatType.SpiritRating);

        hero.Initialize();
        hero.Abilities.AddRange(CommonAbilities.For(hero));
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

        // The relic passives before gems: Covetous Greed raises every gem colour's power, rounded down (combat logs: 350
        // Topaz is 437).
        hero.Relic1 = BindRelic(hero, build.Relic1ItemId);
        hero.Relic2 = BindRelic(hero, build.Relic2ItemId);

        double gemPowerMultiplier = hero.RelicPassives.Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        foreach (var (color, power) in build.GemPower)
        {
            var gem = Gem.Create(color);
            gem.Power = Math.Floor(power * gemPowerMultiplier);
            hero.Gems.Add(gem);
            gem.Bind(hero);
            hero.UnmodeledFeatures.AddRange(gem.UnmodeledNodes.Select(n => $"Gem node: {n}"));
        }
        // Gem power over the cap raises Main Stat and Stamina.
        double overcap = Gem.OvercapBonusPerPower * hero.Gems.Sum(g => Math.Max(0.0, g.Power - Gem.OvercapPower));
        if (overcap > 0)
        {
            hero.PassiveAuras.Add(AuraDef.Internal("Gem power overcap", 0, 1, StatModifier.Increased(StatType.PrimaryAttributeMultiplier, 1.0 + overcap)));
            hero.Stats.StaminaMultiplier *= 1.0 + overcap;
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
            hero.PassiveAuras.Add(LegendaryStats.MoteOfCreation);
        }

        hero.FinalizeBuild();
        return hero;
    }
}
