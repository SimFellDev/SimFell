using FellowshipSim.Core.Customization.Aspects;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Sapphire Aurastone (aspect 32): "Your Spirit Ability causes you to conjure a Sapphire Aurastone. It lasts as long as the Spirit of
/// Heroism effect from your Spirit Ability is active on you. While active, 3.4% / 6.3% / 9.1% / 12% of all damage you deal is accumulated
/// into the Sapphire Aurastone and released in a pulse of damage, evenly split between nearby enemies every 3 seconds" (ranks 1 to 4). The
/// first pulse comes 6 seconds after the stone appears, from combat logs.
/// </summary>
public class AspectSapphireAurastone
{
    private const string Pulse = "Sapphire Aurastone Damage";

    /// <summary>
    /// Casts <paramref name="ultimate"/>, then <paramref name="ability"/> until <paramref name="seconds"/> after the stone appears; returns
    /// when it appeared.
    /// </summary>
    private static double UltimateThenHits(StepDriver f, string ultimate, string ability, double seconds)
    {
        f.FillResources(); // enough to cast it
        f.Cast(ultimate);
        double start = f.CompletedAt(ultimate);
        while (f.Seconds() < start + seconds)
        {
            f.FillResources(); // enough to cast it
            f.CastWhenReady(ability);
        }
        return start;
    }

    /// <summary>
    /// A pulse deals 3.4% / 6.3% / 9.1% / 12% of the damage you dealt since the pulse before.
    ///
    /// The second pulse, with the hits timed between the two pulses: no hit lands at a pulse's instant, which could go to either pulse.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter", "Frost Bolt", 1, 0.034)]
    [InlineData("Rime", "Wrath of Winter", "Frost Bolt", 2, 0.063)]
    [InlineData("Rime", "Wrath of Winter", "Frost Bolt", 3, 0.091)]
    [InlineData("Rime", "Wrath of Winter", "Frost Bolt", 4, 0.12)]
    [InlineData("Ardeos", "Incinerate", "Infernal Wave", 4, 0.12)]
    [InlineData("Elarion", "Event Horizon", "Focused Shot", 4, 0.12)]
    [InlineData("Gunde", "Bloodbound Spirit", "Double Strike", 4, 0.12)]
    [InlineData("Mara", "Matriarch Macabre", "Skittering Blades", 4, 0.12)]
    [InlineData("Tariq", "Raging Tempest", "Heavy Strike", 4, 0.12)]
    public void A_Sapphire_Aurastone_Pulse_Deals_A_Share_Of_Your_Damage(string hero, string ultimate, string ability, int rank,
        double share)
    {
        var f = StepFights.Of(hero, augmentations: [new(SapphireAurastone.AspectId, rank)]).WithoutDamageSpread();
        f.FillResources(); // enough to cast it

        f.Cast(ultimate);
        double start = f.CompletedAt(ultimate);
        f.FillResources(); // enough to cast it
        f.Cast(ability); // damage for the first pulse
        f.WaitUntil(start + 6.5); // after the first pulse
        f.FillResources(); // enough to cast it
        f.Cast(ability);
        f.WaitUntil(start + 9.5); // the second pulse

        var pulses = f.Damage(Pulse);
        double dealtBetween = f.DamageBetween(pulses[0].Time, pulses[1].Time, except: Pulse).Sum(hit => hit.Value);

        using var check = new Checks(f);
        check.False(f.AllDamage().Any(hit => hit.Name != Pulse && (hit.Time == pulses[0].Time || hit.Time == pulses[1].Time)),
            "a hit lands with a pulse");
        check.True(dealtBetween > 0, "damage dealt between the pulses");
        check.Ratio(share, pulses[1].Value / dealtBetween);
    }

    /// <summary>
    /// The stone pulses 6 seconds after it appears, then every 3 seconds while Spirit of Heroism lasts: 5 pulses in its 20 seconds.
    ///
    /// Times are checked within 0.05 seconds: an effect ends on its server frame.
    /// </summary>
    [Fact]
    public void The_Sapphire_Aurastone_Pulses_At_6_Seconds_Then_Every_3_While_Spirit_Of_Heroism_Lasts()
    {
        var f = StepFights.Of("Rime", augmentations: [new(SapphireAurastone.AspectId, 4)]);

        double start = UltimateThenHits(f, "Wrath of Winter", "Frost Bolt", seconds: 19.5);
        f.WaitUntil(start + 25);

        var pulses = f.Damage(Pulse);

        using var check = new Checks(f);
        check.Equal(5, pulses.Count);
        for (int i = 0; i < pulses.Count; i++) check.Equal(start + 6 + 3 * i, pulses[i].Time.TotalSeconds, 0.05, what: $"pulse {i + 1}");
    }

    /// <summary>A pulse is split evenly between the enemies.</summary>
    [Fact]
    public void A_Sapphire_Aurastone_Pulse_Is_Split_Evenly()
    {
        var f = StepFights.Of("Rime", enemies: 3, augmentations: [new(SapphireAurastone.AspectId, 4)]).WithoutDamageSpread();

        UltimateThenHits(f, "Wrath of Winter", "Frost Bolt", seconds: 7);

        var pulses = f.FirstHitOnEachEnemy(Pulse);

        using var check = new Checks(f);
        check.Equal(3, pulses.Count);
        foreach (double pulse in pulses) check.Ratio(1.0, pulse / pulses[0]);
    }

    /// <summary>A pulse never crits.</summary>
    [Fact]
    public void A_Sapphire_Aurastone_Pulse_Never_Crits()
    {
        var f = StepFights.Of("Rime", crit: 1.0, augmentations: [new(SapphireAurastone.AspectId, 4)]);

        UltimateThenHits(f, "Wrath of Winter", "Frost Bolt", seconds: 7);

        Assert.Equal(0, f.Damage(Pulse)[0].CritChance, 1e-9);
    }
}
