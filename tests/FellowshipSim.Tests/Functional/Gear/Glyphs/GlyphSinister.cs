using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Sinister (glyph 701): "Your Core abilities have 1% / 2% / 3% / 4% increased Critical Strike chance and 5% / 10% / 15% / 20%
/// increased Critical Strike power" (ranks 1 to 4). Each rule runs on every hero's Core ability at rank 4, and on Rime's at the other
/// ranks.
/// </summary>
public class GlyphSinister
{
    /// <summary>The first hit of <paramref name="ability"/>, with 20% crit, with or without the glyph.</summary>
    private static StepEvent FirstHit(string hero, string ability, int rank, bool? crit)
    {
        var f = StepFights.Of(hero, crit: 0.2, glyphs: StepFights.Ranked(TheSinister.GlyphId, rank)).WithoutDamageSpread();
        f.FillResources(); // enough to cast it

        f.Cast(ability, crit: crit);
        f.Wait(3); // the hits land

        return f.Damage(ability)[0];
    }

    /// <summary>Your Core abilities' hits have The Sinister's crit chance on top of yours: 1% / 2% / 3% / 4%.</summary>
    [Theory]
    [InlineData("Rime", "Freezing Torrent", 1, 0.01)]
    [InlineData("Rime", "Freezing Torrent", 2, 0.02)]
    [InlineData("Rime", "Freezing Torrent", 3, 0.03)]
    [InlineData("Rime", "Freezing Torrent", 4, 0.04)]
    [InlineData("Ardeos", "Fire Ball", 4, 0.04)]
    [InlineData("Elarion", "Multishot", 4, 0.04)]
    [InlineData("Gunde", "Blood Arc", 4, 0.04)]
    [InlineData("Mara", "Widow's Bite", 4, 0.04)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.04)]
    public void The_Sinister_Adds_Crit_Chance_To_Core_Abilities(string hero, string core, int rank, double critChance)
    {
        var hit = FirstHit(hero, core, rank, crit: false);

        Assert.Equal(0.2 + critChance, hit.CritChance, 1e-9);
    }

    /// <summary>Your Core abilities' critical strikes are stronger by The Sinister's crit power: 5% / 10% / 15% / 20%.</summary>
    [Theory]
    [InlineData("Rime", "Freezing Torrent", 1, 0.05)]
    [InlineData("Rime", "Freezing Torrent", 2, 0.10)]
    [InlineData("Rime", "Freezing Torrent", 3, 0.15)]
    [InlineData("Rime", "Freezing Torrent", 4, 0.20)]
    [InlineData("Ardeos", "Fire Ball", 4, 0.20)]
    [InlineData("Elarion", "Multishot", 4, 0.20)]
    [InlineData("Gunde", "Blood Arc", 4, 0.20)]
    [InlineData("Mara", "Widow's Bite", 4, 0.20)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.20)]
    public void The_Sinister_Adds_Crit_Power_To_Core_Abilities(string hero, string core, int rank, double critPower)
    {
        double plainCrit = FirstHit(hero, core, rank: 0, crit: true).Value;
        double sinisterCrit = FirstHit(hero, core, rank, crit: true).Value;

        AssertRatio(1 + critPower, sinisterCrit / plainCrit);
    }

    /// <summary>The Sinister leaves your other abilities alone.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Ice Comet")]
    public void The_Sinister_Leaves_Other_Hits_Alone(string ability)
    {
        var plainCrit = FirstHit("Rime", ability, rank: 0, crit: true);
        var sinisterCrit = FirstHit("Rime", ability, rank: 4, crit: true);

        using var check = new Checks();
        check.Equal(plainCrit.CritChance, sinisterCrit.CritChance);
        check.Ratio(1.0, sinisterCrit.Value / plainCrit.Value);
    }

    // ---- Core hits a talent triggers

    /// <summary>
    /// The Sinister reaches the Bursting Ice you cast, but not the Bursting Ice pulses Bursting Swallows triggers.
    ///
    /// Per the user. Combat logs show Bursting Ice's pulses without the glyph: those of a talent.
    /// </summary>
    [Theory]
    [InlineData(true, 0.24)]
    [InlineData(false, 0.2)]
    public void The_Sinister_Reaches_A_Cast_Bursting_Ice_But_Not_A_Bursting_Swallows_Pulse(bool cast, double critChance)
    {
        var f = StepFights.Of("Rime", crit: 0.2, talents: [BurstingSwallows.TalentId], glyphs: [new(TheSinister.GlyphId, 4)]);
        f.ForceProcs(key => key == BurstingSwallows.ProcKey ? true : null);

        if (cast)
        {
            f.Cast("Bursting Ice");
        }
        else
        {
            f.FillResources(); // the bolt's Anima sends Frost Swallows
            f.Cast("Frost Bolt");
        }
        f.Wait(3); // the pulses

        var pulses = f.Damage("Bursting Ice");

        using var check = new Checks(f);
        check.True(pulses.Count > 0);
        foreach (var pulse in pulses) check.Equal(critChance, pulse.CritChance, what: $"pulse at {pulse.Time.TotalSeconds:F3}s");
    }

    /// <summary>
    /// The Sinister reaches the frogs of the Fire Frogs you cast, but not the frogs Pyrophibian Frenzy spawns.
    ///
    /// Per the user.
    /// </summary>
    [Theory]
    [InlineData(true, 0.24)]
    [InlineData(false, 0.2)]
    public void The_Sinister_Reaches_A_Cast_Fire_Frogs_But_Not_A_Pyrophibian_Frenzy_Frog(bool cast, double critChance)
    {
        var f = StepFights.Of("Ardeos", crit: 0.2, talents: [PyrophibianFrenzy.TalentId], glyphs: [new(TheSinister.GlyphId, 4)]);
        f.ForceProcs(key => key == PyrophibianFrenzy.ProcKey ? !cast : null);

        f.Cast(cast ? "Fire Frogs" : "Searing Blaze");
        f.Wait(6); // the frogs attack

        // the frogs' attacks, logged under their effect, without the burn they leave, also named Fire Frogs
        var attacks = f.Ticks(FireFrogAttack.DamageEffectId);

        using var check = new Checks(f);
        check.True(attacks.Count > 0);
        foreach (var attack in attacks) check.Equal(critChance, attack.CritChance, what: $"attack at {attack.Time.TotalSeconds:F3}s");
    }
}
