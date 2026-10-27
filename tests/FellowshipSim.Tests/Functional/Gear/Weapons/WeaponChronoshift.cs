using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>
/// Chronoshift (Asha's Chronoshift Spire): a 1 second cast, then a 3 second channel that pulses at all nearby enemies every 1.5 seconds. While
/// channeling, Asha's Arcane Embrace halves the damage you take and gives 800% increased cooldown recovery. Cooldown 180 seconds.
/// Dev: damage is the expected value with no crit; a pulse is read as a multiple of Main Stat (1000) over the class tuning. The cast time and the pulse period are
/// hasted. From the logs: a pulse lands as the channel starts (the engine's comes a server frame, 0.033 seconds, after the cast completes), then every hasted
/// period, plus a partial final pulse as the channel ends; pulses lose damage as sqrt(5/N) beyond 5 enemies.
/// </summary>
public class WeaponChronoshift
{
    private const double Ratio = 0.001;
    private const int Id = WeaponHeroes.ChronoshiftId;
    private const string Name = "Chronoshift";

    /// <summary>Player: the cast takes 1 second, shortened by haste. Dev: 0%, 20% and 50% haste.</summary>
    [Theory]
    [MemberData(nameof(CastTimes))]
    public void The_Cast_Time_Is_1_Second_Shortened_By_Haste(GearHero hero, double haste)
    {
        var f = hero.WithWeapon(Id, haste: haste);
        f.Cast(f.Weapon());

        Assert.Equal(1.0 / (1 + haste), f.CompletedAt(Name), 0.002);
    }

    public static IEnumerable<object[]> CastTimes =>
        WeaponHeroes.Chronoshift.SelectMany(h => new[] { 0.0, 0.2, 0.5 }.Select(haste => new[] { h[0], (object)haste }));

    /// <summary>Player: a pulse deals 576.9% of your Main Stat to an enemy. Dev: one enemy, the first pulse.</summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void A_Pulse_Deals_576_9_Percent(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());

        Assert.Equal(5.769, f.Coefficient(f.Damage(Name).First().Value), 5.769 * Ratio);
    }

    /// <summary>
    /// Player: past 5 enemies each pulse loses damage: every enemy takes sqrt(5/N) of it.
    /// Dev: 7 enemies: each is hit by each pulse for 576.9% x sqrt(5/7), and the pulse hits all 7.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void Pulses_Lose_Damage_Beyond_Five_Enemies(GearHero hero)
    {
        var f = hero.WithWeapon(Id, enemies: 7);
        f.Cast(f.Weapon());

        var first = f.Damage(Name).Take(7).ToList();
        Assert.Equal(7, first.Select(h => h.Target).Distinct().Count());
        double each = 5.769 * Math.Sqrt(5.0 / 7.0);
        foreach (var hit in first) Assert.Equal(each, f.Coefficient(hit.Value), each * Ratio);
    }

    /// <summary>
    /// Player: it pulses as the channel starts and every 1.5 seconds after (shortened by haste), then once more as the channel ends: 3 pulses at no haste.
    /// Dev: no haste; the first pulse within a frame of the cast completing, the second 1.5 seconds after it, the last as the 3 second channel ends.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void It_Pulses_At_The_Start_Every_1_5_Seconds_And_At_The_End(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(5);

        double start = f.CompletedAt(Name);
        var pulses = f.Damage(Name).Select(e => e.Time.TotalSeconds).ToList();
        Assert.Equal(3, pulses.Count);
        Assert.Equal(start, pulses[0], 0.05);
        Assert.Equal(pulses[0] + 1.5, pulses[1], 0.002);
        Assert.Equal(start + 3.0, pulses[2], 0.05);
    }

    /// <summary>
    /// Player: haste makes the pulses come sooner (every 1.5 / (1 + haste) seconds) while the channel stays 3 seconds: 4 pulses at 50% haste, 1 second apart,
    /// the last as the channel ends.
    /// Dev: 50% haste.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void Haste_Shortens_The_Pulse_Period(GearHero hero)
    {
        var f = hero.WithWeapon(Id, haste: 0.5);
        f.Cast(f.Weapon());
        f.Wait(5);

        double start = f.CompletedAt(Name);
        var pulses = f.Damage(Name).Select(e => e.Time.TotalSeconds).ToList();
        Assert.Equal(4, pulses.Count);
        Assert.Equal(pulses[0] + 1.0, pulses[1], 0.002);
        Assert.Equal(pulses[0] + 2.0, pulses[2], 0.002);
        Assert.Equal(start + 3.0, pulses[3], 0.05);
    }

    /// <summary>
    /// Player: the last pulse is partial when the channel ends between two pulses: it deals the share of a pulse the time since the last one is of the period.
    /// Dev: 20% haste, period 1.25 seconds: pulses at 0, 1.25, 2.5 and the last at 3.0, a fifth (0.5/1.25 = 40%) of a pulse. From the logs; the engine's
    /// partial pulse is its channel's own rule.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void The_Final_Pulse_Is_Partial(GearHero hero)
    {
        var f = hero.WithWeapon(Id, haste: 0.2);
        f.Cast(f.Weapon());
        f.Wait(5);

        var pulses = f.Damage(Name);
        Assert.Equal(4, pulses.Count);
        Assert.Equal(5.769, f.Coefficient(pulses[0].Value), 5.769 * Ratio);
        Assert.Equal(5.769 * 0.4, f.Coefficient(pulses[3].Value), 5.769 * 0.4 * Ratio);
    }

    /// <summary>
    /// Player: Asha's Arcane Embrace is up for the 3 seconds of the channel, from the moment it starts.
    /// Dev: the aura is gained as the cast completes and removed 3 seconds later.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void The_Barrier_Lasts_As_Long_As_The_Channel(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        f.Cast(f.Weapon());
        f.Wait(5);

        double gained = Assert.Single(f.GainedAt("Asha's Arcane Embrace"));
        double removed = Assert.Single(f.RemovedAt("Asha's Arcane Embrace"));
        Assert.Equal(f.CompletedAt(Name), gained, 0.002);
        Assert.Equal(3.0, removed - gained, 0.002);
    }

    /// <summary>
    /// Player: while you channel, the barrier gives you +700% cooldown acceleration (the tooltip says 800%, which is wrong): your other cooldowns recover at 8 times the speed.
    /// Dev: a Major ability of the hero is used first, then Chronoshift: over the 3 seconds of the channel the first ability's cooldown has run down 24
    /// cooldown seconds (counted in cooldown seconds, as the hero reports the time left at the current recovery speed).
    /// The rule is settled by the user: +700% cooldown acceleration, so 1 + 7 = 8 times; the tooltip's 800% is a mistake.
    /// </summary>
    [Theory]
    [MemberData(nameof(WeaponHeroes.Chronoshift), MemberType = typeof(WeaponHeroes))]
    public void The_Barrier_Speeds_Up_Other_Cooldowns_Eightfold(GearHero hero)
    {
        var f = hero.WithWeapon(Id);
        GearFights.Fill(f);
        var other = hero.Get(f, hero.Major);
        f.Cast(other);
        var before = f.CooldownAtCompletion(other, f.Weapon());
        f.Cast(f.Weapon());
        double channelEnd = f.CompletedAt(Name) + 3.0;
        f.WaitUntil(channelEnd);

        Assert.Equal(24.0, before() - f.CooldownSecondsLeft(other), 0.05);
    }
}
