using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Hunter's Focus (sigil 42): "Your targeted offensive abilities grant you 4 / 8 / 12 / 16 Haste rating for 8 seconds, stacking up to 5
/// times. The Haste is lost if you use a targeted offensive ability on any other enemy" (ranks 1 to 4). A rating point is 0.16%.
/// </summary>
public class SigilHuntersFocus
{
    /// <summary>Your targeted offensive abilities give you 4 / 8 / 12 / 16 Haste rating.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 4)]
    [InlineData("Rime", "Frost Bolt", 2, 8)]
    [InlineData("Rime", "Frost Bolt", 3, 12)]
    [InlineData("Rime", "Frost Bolt", 4, 16)]
    [InlineData("Ardeos", "Infernal Wave", 4, 16)]
    [InlineData("Elarion", "Focused Shot", 4, 16)]
    [InlineData("Gunde", "Double Strike", 4, 16)]
    [InlineData("Mara", "Backstab", 4, 16)]
    [InlineData("Tariq", "Heavy Strike", 4, 16)]
    public void Hunters_Focus_Targeted_Abilities_Give_Haste_Rating(string hero, string ability, int rank, double rating)
    {
        var f = StepFights.Of(hero, augmentations: [new(HuntersFocus.SigilId, rank)]);

        f.Cast(ability);

        Assert.Equal(rating * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>Hunter's Focus stacks up to 5 times: a 6th ability adds nothing.</summary>
    [Fact]
    public void Hunters_Focus_Stacks_Up_To_5_Times()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HuntersFocus.SigilId, 1)]);

        for (int cast = 0; cast < 6; cast++) f.Cast("Frost Bolt");

        Assert.Equal(5 * 4 * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>Hunter's Focus lasts 8 seconds.</summary>
    [Fact]
    public void Hunters_Focus_Lasts_8_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HuntersFocus.SigilId, 1)]);

        f.Cast("Frost Bolt");
        double start = f.CompletedAt("Frost Bolt");

        f.WaitUntil(start + 8 - 0.1);
        Assert.Equal(4 * 0.0016, f.Haste(), 1e-9);
        f.WaitUntil(start + 8 + 0.1);
        Assert.Equal(0, f.Haste(), 1e-9);
    }

    /// <summary>A targeted offensive ability on another enemy loses the stacks, and starts again from one.</summary>
    [Fact]
    public void Another_Enemy_Loses_The_Hunters_Focus_Stacks()
    {
        var f = StepFights.Of("Rime", enemies: 2, augmentations: [new(HuntersFocus.SigilId, 1)]);

        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt", target: f.Enemies()[1]);

        Assert.Equal(4 * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>Hunter's Focus's buff, by its name and effect ID (an aura's stacks are kept under both).</summary>
    private static readonly AuraDef FocusedHaste =
        AuraDef.Effect("Trait : Focused Haste", HuntersFocus.BuffEffectId, HuntersFocus.DurationSeconds, HuntersFocus.MaxStacks);

    /// <summary>
    /// Every ability of each hero, and whether it gives Hunter's Focus. Only a targeted offensive ability does: a hero ability cast on an
    /// enemy you select. One cast on the ground (Fire Ball, Apocalypse, Leap Smash, Raging Tempest) does not, nor one with no target, a
    /// Control ability, an interrupt, a movement ability (Stalker Step too), an auto-attack, or anything that is not an ability you cast
    /// (Frost Swallows). The relic abilities, Mount, Making Camp and the weapon abilities, which every hero can have, are checked on Rime
    /// alone.
    /// </summary>
    [Theory]
    
    [InlineData("Ardeos", "Infernal Wave", true)]
    [InlineData("Ardeos", "Engulfing Flames", true)]
    [InlineData("Ardeos", "Searing Blaze", true)]
    [InlineData("Ardeos", "Pyromania", true)]
    [InlineData("Ardeos", "Fire Frogs", true)]
    [InlineData("Ardeos", "Fire Ball", false)]
    [InlineData("Ardeos", "Apocalypse", false)]
    [InlineData("Ardeos", "Detonate", false)]
    [InlineData("Ardeos", "Incinerate", false)]
    [InlineData("Ardeos", "Wildfire", false)]

    [InlineData("Rime", "Frost Bolt", true)]
    [InlineData("Rime", "Cold Snap", true)]
    [InlineData("Rime", "Freezing Torrent", true)]
    [InlineData("Rime", "Glacial Blast", true)]
    [InlineData("Rime", "Bursting Ice", true)]
    [InlineData("Rime", "Ice Comet", true)]
    [InlineData("Rime", "Talon Strike", true)]
    [InlineData("Rime", "Rising Talons", true)]
    [InlineData("Rime", "Ice Blitz", false)]
    [InlineData("Rime", "Wrath of Winter", false)]
    [InlineData("Rime", "Winter's Blessing", false)]
    [InlineData("Rime", "Flight of the Navir", false)]
    [InlineData("Rime", "Frost Swallows", false)]
    [InlineData("Rime", "Frost Ward", false)]
    [InlineData("Rime", "Ice Dash", false)]
    [InlineData("Rime", "Frigid Winds", false)]
    [InlineData("Rime", "Brain Freeze", false)]
    
    [InlineData("Elarion", "Focused Shot", true)]
    [InlineData("Elarion", "Celestial Shot", true)]
    [InlineData("Elarion", "Heartseeker Barrage", true)]
    [InlineData("Elarion", "Highwind Arrow", true)]
    [InlineData("Elarion", "Starfall Volley", true)]
    [InlineData("Elarion", "Multishot", true)]
    [InlineData("Elarion", "Lunarlight Mark", true)]
    [InlineData("Elarion", "Event Horizon", false)]
    [InlineData("Elarion", "Skystrider's Grace", false)]
    [InlineData("Elarion", "Skystrider's Supremacy", false)]
    [InlineData("Elarion", "Shoot", false)]
    
    [InlineData("Gunde", "Double Strike", true)]
    [InlineData("Gunde", "Grim Carve", true)]
    [InlineData("Gunde", "Heart Splitter", true)]
    [InlineData("Gunde", "Rupture", true)]
    [InlineData("Gunde", "Blood Arc", false)]
    [InlineData("Gunde", "Reaver's Edge", false)]
    [InlineData("Gunde", "Slaughter", false)]
    [InlineData("Gunde", "Bloodbound Spirit", false)]
    [InlineData("Gunde", "Warbound", false)]
    [InlineData("Gunde", "Reign in Blood", false)]
    [InlineData("Gunde", "Reckless Abandon", false)]
    [InlineData("Gunde", "Owed in Blood", false)]
    [InlineData("Gunde", "Butcher's Hook", false)]
    [InlineData("Gunde", "Jawbreaker", false)]
    [InlineData("Gunde", "Attack", false)]

    [InlineData("Mara", "Backstab", true)]
    [InlineData("Mara", "Hemorrhaging Strike", true)]
    [InlineData("Mara", "Queen's Fang", true)]
    [InlineData("Mara", "Widow's Bite", true)]
    [InlineData("Mara", "Stealth Backstab", true)]

    [InlineData("Mara", "Skittering Blades", false)]
    [InlineData("Mara", "Arachnid Assault", false)]
    [InlineData("Mara", "Maiden of Death", false)]
    [InlineData("Mara", "Matriarch Macabre", false)]
    [InlineData("Mara", "Final Stratagem", false)]
    [InlineData("Mara", "Macabre Stratagem", false)]
    [InlineData("Mara", "Shadow Ward", false)]
    [InlineData("Mara", "Brooding Shadows", false)]
    [InlineData("Mara", "Stealth Skittering Blades", false)]
    [InlineData("Mara", "Stalker Step", false)]
    [InlineData("Mara", "Web Crack", false)]
    [InlineData("Mara", "Kick", false)]
    [InlineData("Mara", "Attack", false)]

    [InlineData("Tariq", "Face Breaker", true)]
    [InlineData("Tariq", "Heavy Strike", true)]
    [InlineData("Tariq", "Skull Crusher", true)]
    [InlineData("Tariq", "Culling Strike", true)]
    [InlineData("Tariq", "Chain Lightning", true)]
    [InlineData("Tariq", "Wild Swing", true)]
    [InlineData("Tariq", "Leap Smash", false)]
    [InlineData("Tariq", "Raging Tempest", false)]
    [InlineData("Tariq", "Hammer Storm", false)]
    [InlineData("Tariq", "Thunder Call", false)]
    [InlineData("Tariq", "Focused Wrath", false)]

    // Neutral abilities
    [InlineData("Rime", "Revive", false)]
    [InlineData("Rime", "Major Dispel", false)]
    [InlineData("Rime", "Chickenize!", false)]
    [InlineData("Rime", "Major Invisibility", false)]
    [InlineData("Rime", "Relic Teleport", false)]
    [InlineData("Rime", "Bloodrite Fervor", false)]
    [InlineData("Rime", "Conjure Portal", false)]
    [InlineData("Rime", "Rejuvenate", false)]
    [InlineData("Rime", "Anchor", false)]
    [InlineData("Rime", "Making Camp", false)]
    [InlineData("Rime", "Mount", false)]

    // Weapon abilities
    [InlineData("Rime", "Al'zerac's Shackle", true)]
    [InlineData("Rime", "Fated Strike", true)]
    [InlineData("Rime", "Nature's Fury", true)]
    [InlineData("Rime", "Voidbringer's Touch", true)]
    [InlineData("Rime", "Zeraleth's Hunger", true)]
    [InlineData("Rime", "Chronoshift", false)]
    [InlineData("Rime", "Icicles of An'zhyr", false)]
    [InlineData("Rime", "Sahril's Wrath", false)]
    [InlineData("Rime", "Sahril's Aegis", false)]
    public void Hunters_Focus_Comes_From_Targeted_Offensive_Abilities_Only(string hero, string ability, bool gives)
    {
        var f = StepFights.Of(hero, talents: TalentsFor(ability), weaponAbilityId: WeaponAbilities.GetValueOrDefault(ability),
            augmentations: [new(HuntersFocus.SigilId, 1)]);
        f.FillResources(); // enough to cast it
        Prepare(f, ability);
        int before = f.Stacks(FocusedHaste);

        f.Cast(ability);

        Assert.Equal(gives ? before + 1 : before, f.Stacks(FocusedHaste));
    }

    /// <summary>Every weapon ability, by name, with its ID.</summary>
    private static readonly Dictionary<string, int> WeaponAbilities = typeof(NaturesFury).Assembly.GetTypes()
        .Where(type => type.Namespace == typeof(NaturesFury).Namespace && typeof(FellowshipSim.Core.Domain.Ability).IsAssignableFrom(type) && !type.IsAbstract && !type.IsNested)
        .ToDictionary(type => (string)type.GetField("AbilityName")!.GetValue(null)!, type => (int)type.GetField("AbilityId")!.GetValue(null)!);

    /// <summary>The talents an ability needs to be cast.</summary>
    private static int[]? TalentsFor(string ability) => ability switch
    {
        "Talon Strike" or "Rising Talons" => [IcyTalons.TalentId],
        "Macabre Stratagem" => [MacabreStratagemTalent.TalentId],
        _ => null,
    };

    /// <summary>What an ability needs before it can be cast.</summary>
    private static void Prepare(StepDriver f, string ability)
    {
        switch (ability)
        {
            case "Detonate": f.Cast("Searing Blaze"); break; // Detonate only hits a burning enemy
            case "Chain Lightning": f.Cast("Thunder Call"); break; // Chain Lightning needs Thunder Call
            case "Culling Strike": f.SetHealth(0.3); break; // Culling Strike needs an enemy at 30% health or less
            case "Stealth Backstab" or "Stealth Skittering Blades": f.Cast("Brooding Shadows"); break; // they need Stealth
        }
    }

    /// <summary>The theory above lists every ability of "Ardeos", "Rime", "Elarion", "Gunde", "Mara", "Tariq", and every weapon ability.</summary>
    [Fact]
    public void Every_Ability_Is_Checked_For_Hunters_Focus()
    {
        var listed = typeof(SigilHuntersFocus).GetMethod(nameof(Hunters_Focus_Comes_From_Targeted_Offensive_Abilities_Only))!
            .GetCustomAttributes(typeof(InlineDataAttribute), false).Cast<InlineDataAttribute>()
            .Select(row => ((string)row.GetData(null!).First()[0], (string)row.GetData(null!).First()[1])).ToHashSet();
        var missing = new[] { "Ardeos", "Rime", "Elarion", "Gunde", "Mara", "Tariq" }
            .SelectMany(hero => StepFights.Of(hero).Hero.Abilities.Select(ability => (hero, ability.Name)))
            .Concat(WeaponAbilities.Keys.Select(name => ("Rime", name)))
            .Where(entry => !listed.Contains(entry) && !listed.Contains(("Rime", entry.Item2)))  // abilities every hero has are listed on Rime
            .Distinct().ToList();

        Assert.True(missing.Count == 0, "not listed: " + string.Join(", ", missing));
    }
}
