using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Chronoshift (Asha's Chronoshift Spire), over every hero that can have it (Rime, Ardeos, Elarion): a 1 second cast, then a 3 second
/// channel that pulses at nearby enemies every 1.5 seconds, while Asha's Arcane Embrace halves the damage you take and speeds up your other
/// cooldowns.
///
/// Its cooldown is in WeaponCooldowns.cs. The barrier's damage taken is not tested: the sim has no damage taken.
/// </summary>
public class WeaponChronoshift
{
    /// <summary>Chronoshift's cast takes 1 second, shortened by haste.</summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.2)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.2)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.2)]
    [InlineData("Elarion", 0.5)]
    public void Chronoshifts_Cast_Takes_1_Second_Shortened_By_Haste(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: Chronoshift.AbilityId);

        f.Cast<Chronoshift>();

        AssertSeconds(1.0 / (1 + haste), f.LastCastDuration("Chronoshift"));
    }

    /// <summary>
    /// A Chronoshift pulse deals 576.9% of your main stat to each enemy, times your class's balancing aura (Rime 1.04, Ardeos 1.1,
    /// Elarion 1.08).
    /// </summary>
    [Theory]
    [InlineData("Rime", 1.04)]
    [InlineData("Ardeos", 1.1)]
    [InlineData("Elarion", 1.08)]
    public void A_Chronoshift_Pulse_Deals_576_9_Percent_Main_Stat(string hero, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: Chronoshift.AbilityId).WithoutDamageSpread();

        f.Cast<Chronoshift>();

        AssertRatio(5.769 * balancing, f.Damage<Chronoshift>()[0].Value / f.MainStat());
    }

    /// <summary>Beyond 5 enemies, each enemy takes sqrt(5 / enemies) of a Chronoshift pulse.</summary>
    [Theory]
    [InlineData("Rime", 5)]
    [InlineData("Rime", 7)]
    [InlineData("Ardeos", 5)]
    [InlineData("Ardeos", 7)]
    [InlineData("Elarion", 5)]
    [InlineData("Elarion", 7)]
    public void Chronoshift_Pulses_Lose_Damage_Beyond_5_Enemies(string hero, int enemies)
    {
        List<double> FirstPulseOnEachEnemy(int enemies)
        {
            var f = StepFights.Of(hero, enemies: enemies, weaponAbilityId: Chronoshift.AbilityId).WithoutDamageSpread();

            f.Cast<Chronoshift>();

            return f.FirstHitOnEachEnemy("Chronoshift");
        }

        double alonePulse = FirstPulseOnEachEnemy(1).Single();
        var crowdPulses = FirstPulseOnEachEnemy(enemies);

        using var check = new Checks();
        check.Equal(enemies, crowdPulses.Count);
        foreach (double pulse in crowdPulses) check.Ratio(Math.Min(1, Math.Sqrt(5.0 / enemies)), pulse / alonePulse);
    }

    /// <summary>
    /// Chronoshift pulses as its channel starts, then every 1.5 seconds shortened by haste, and once more as the 3 second channel ends when
    /// that falls between two pulses: 3 pulses at no haste, 4 at 20% or 50% haste.
    ///
    /// The first pulse comes a server frame (0.033 seconds) after the channel starts, so times are checked within 0.05 seconds.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.2)]
    [InlineData("Rime", 0.5)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.2)]
    [InlineData("Ardeos", 0.5)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.2)]
    [InlineData("Elarion", 0.5)]
    public void Chronoshift_Pulses_As_The_Channel_Starts_Then_Every_1_5_Hasted_Seconds_And_As_It_Ends(string hero, double haste)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: Chronoshift.AbilityId);

        f.Cast<Chronoshift>();
        f.Wait(1); // the channel is over

        double start = f.CompletedAt("Chronoshift");
        double period = 1.5 / (1 + haste);
        var pulses = f.Damage<Chronoshift>();

        using var check = new Checks(f);
        check.Equal(1 + FullTicks(3.0, period) + PartialTick(3.0, period), pulses.Count);
        for (int i = 0; i <= FullTicks(3.0, period); i++)
        {
            check.Equal(start + i * period, pulses[i].Time.TotalSeconds, 0.05, what: $"pulse {i + 1}");
        }
        check.Equal(start + 3.0, pulses[^1].Time.TotalSeconds, 0.05, what: "the last pulse");
    }

    /// <summary>
    /// When Chronoshift's channel ends between two pulses, its last pulse deals the share of a pulse that the time since the one before is
    /// of the period: at 20% haste, pulses come every 1.25 seconds, and the last, 0.5 seconds after the one before, deals 40% of a pulse.
    ///
    /// From combat logs.
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void Chronoshifts_Last_Pulse_Deals_Its_Share_Of_A_Pulse(string hero)
    {
        var f = StepFights.Of(hero, haste: 0.2, weaponAbilityId: Chronoshift.AbilityId).WithoutDamageSpread();

        f.Cast<Chronoshift>();
        f.Wait(1); // the channel is over

        var pulses = f.Damage<Chronoshift>();
        double period = 1.5 / 1.2;
        double share = (3.0 - FullTicks(3.0, period) * period) / period;

        AssertRatio(share, pulses[^1].Value / pulses[0].Value);
    }

    /// <summary>Asha's Arcane Embrace lasts as long as Chronoshift's 3 second channel.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    public void Ashas_Arcane_Embrace_Lasts_The_3_Second_Channel(string hero)
    {
        var f = StepFights.Of(hero, weaponAbilityId: Chronoshift.AbilityId);

        // returns at once, so the barrier can be read during the channel
        f.StartCast<Chronoshift>();
        f.Wait(1.5); // the cast is over, the channel under way
        double start = f.CompletedAt("Chronoshift");

        f.WaitUntil(start + 3 - 0.1);
        Assert.True(f.Has(Chronoshift.Barrier));
        f.WaitUntil(start + 3 + 0.1);
        Assert.False(f.Has(Chronoshift.Barrier));
    }

    /// <summary>
    /// Asha's Arcane Embrace gives +700% cooldown acceleration: your other cooldowns recover 8 times as fast during the channel, so its 3
    /// seconds take 24 seconds off them.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz", 60.0)]
    [InlineData("Ardeos", "Apocalypse", 45.0)]
    [InlineData("Elarion", "Lunarlight Mark", 40.0)]
    public void Ashas_Arcane_Embrace_Makes_Other_Cooldowns_Recover_8_Times_As_Fast(string hero, string major, double cooldown)
    {
        var f = StepFights.Of(hero, weaponAbilityId: Chronoshift.AbilityId);
        f.FillResources(); // enough to cast it

        f.Cast(major);
        f.Cast<Chronoshift>();
        double end = f.CompletedAt(major) + cooldown - 3 * (8 - 1);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(major));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(major));
    }
}
