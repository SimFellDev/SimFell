using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's abilities, on a bare Rime (no talents unless the test is about one): each ability's own rule first, then how it works with the
/// others. Resource gains and costs, cast times and cooldowns have their own files; Spirit Refund, which every hero shares, is in
/// Generic/SpiritRefund.cs.
/// </summary>
public class RimeAbilities
{

    // ---- Damage and targeting

    /// <summary>Frost Bolt, Cold Snap, Ice Comet, Glacial Blast, Bursting Ice and Freezing Torrent all deal damage.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Cold Snap")]
    [InlineData("Ice Comet")]
    [InlineData("Glacial Blast")]
    [InlineData("Bursting Ice")]
    [InlineData("Freezing Torrent")]
    public void Damaging_Abilities_Deal_Damage(string ability)
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 2; // enough for Glacial Blast and Ice Comet

        f.Cast(ability);
        f.Wait(4.0); // slow hits land: the comet falls late, Bursting Ice pulses over 3s

        Assert.NotEmpty(f.Damage(ability));
        Assert.All(f.Damage(ability), hit => Assert.True(hit.Value > 0));
    }

    /// <summary>Frost Bolt, Cold Snap, Glacial Blast and Freezing Torrent hit only the enemy you target.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Cold Snap")]
    [InlineData("Glacial Blast")]
    [InlineData("Freezing Torrent")]
    public void Single_Target_Abilities_Hit_Only_The_Chosen_Enemy(string ability)
    {
        var f = StepFights.Rime(enemies: 2);
        f.RimeOf().WinterOrbs = 2; // enough for Glacial Blast
        var second = f.Enemies()[1]; // not the default target, so hitting the default target would show

        f.Cast(ability, target: second);
        f.Wait(4.0);

        Assert.NotEmpty(f.Damage(ability));
        Assert.All(f.Damage(ability), hit => Assert.Equal(second.Name, hit.Target));
    }

    /// <summary>Bursting Ice and Ice Comet hit every enemy in the fight, not only the one you target.</summary>
    [Theory]
    [InlineData("Bursting Ice")]
    [InlineData("Ice Comet")]
    public void Area_Abilities_Hit_Every_Enemy(string ability)
    {
        var f = StepFights.Rime(enemies: 3);
        f.RimeOf().WinterOrbs = 2; // enough for Ice Comet

        f.Cast(ability);
        f.Wait(4.0); // the comet falls late, Bursting Ice pulses over 3s

        Assert.Equal(3, f.Damage(ability).Select(hit => hit.Target).Distinct().Count());
    }

    // ---- Cold Snap (how long its charges take to come back is in RimeCooldowns.cs)

    /// <summary>Cold Snap has 2 charges: you can cast it twice in a row, but not a third time.</summary>
    [Fact]
    public void Cold_Snap_Has_2_Charges()
    {
        var f = StepFights.Rime();
        Assert.Equal(2, f.Charges<ColdSnap>());

        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();

        Assert.Equal(0, f.Charges<ColdSnap>());
        Assert.False(f.TryCast<ColdSnap>().Succeeded);
    }

    // ---- Ice Blitz

    /// <summary>
    /// Ice Blitz makes you deal 20% more damage for 20 seconds. You can cast it while the global cooldown is running, so
    /// it does not make you wait.
    /// </summary>
    [Fact]
    public void Ice_Blitz_Gives_20_Percent_More_Damage_For_20_Seconds_Without_Using_The_Global_Cooldown()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>();
        double plainBoltDamage = plainScenario.Damage<FrostBolt>().Single().Value;

        var blitzScenario = StepFights.Rime().WithoutDamageSpread();

        blitzScenario.Cast<IceBlitz>();

        using var check = new Checks(blitzScenario);
        check.Equal(0.0, blitzScenario.Seconds());
        check.True(blitzScenario.Has(IceBlitz.Buff));

        blitzScenario.Cast<FrostBolt>(); // to measure the bonus
        double blitzBoltDamage = blitzScenario.Damage<FrostBolt>().Single().Value;
        check.Ratio(1.2, blitzBoltDamage / plainBoltDamage);

        blitzScenario.WaitUntil(19.9);
        check.True(blitzScenario.Has(IceBlitz.Buff));
        blitzScenario.WaitUntil(20.1);
        check.False(blitzScenario.Has(IceBlitz.Buff));
    }

    /// <summary>
    /// Every Frost Swallow that hits adds 0.1 seconds to Ice Blitz. A Cold Snap during Ice Blitz sends 3 swallows, so Ice Blitz lasts
    /// 20.3 seconds; with Flight of the Navir up it sends 8, so it lasts 20.8.
    /// </summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 8)]
    public void Each_Swallow_Hit_Extends_Ice_Blitz_By_0_1_Seconds(bool withFlight, int swallowHits)
    {
        var f = StepFights.Rime();

        f.Cast<IceBlitz>();
        double start = f.CompletedAt("Ice Blitz");
        if (withFlight) f.Cast<FlightOfTheNavir>();

        f.Cast<ColdSnap>();
        f.Wait(3.0); // the swallows land
        double end = start + 20.0 + 0.1 * swallowHits;

        using var check = new Checks(f);
        check.Equal(swallowHits, f.Damage("Frost Swallows").Count);
        f.WaitUntil(end - 0.1);
        check.True(f.Has(IceBlitz.Buff));
        f.WaitUntil(end + 0.1);
        check.False(f.Has(IceBlitz.Buff));
    }

    // ---- Winter's Blessing

    /// <summary>
    /// Winter's Blessing gives you 20% more Spirit for 20 seconds. You can cast it while the global cooldown is
    /// running, so it does not make you wait.
    /// </summary>
    [Fact]
    public void Winters_Blessing_Adds_20_Percent_Spirit_For_20_Seconds_Without_Using_The_Global_Cooldown()
    {
        var f = StepFights.Rime();

        using var check = new Checks(f);
        check.Equal(0.0, f.Spirit());

        f.Cast<WintersBlessing>();
        check.Equal(0.0, f.Seconds());
        check.Equal(0.2, f.Spirit());

        f.WaitUntil(19.9);
        check.Equal(0.2, f.Spirit());
        f.WaitUntil(20.1);
        check.Equal(0.0, f.Spirit());
    }

    // ---- Flight of the Navir

    /// <summary>
    /// Flight of the Navir lasts 20 seconds.
    ///
    /// Its 5 circling Frost Swallows only show when something sends them: the next two tests.
    /// </summary>
    [Fact]
    public void Flight_Of_The_Navir_Lasts_20_Seconds()
    {
        var f = StepFights.Rime();

        f.Cast<FlightOfTheNavir>();
        double start = f.CompletedAt("Flight of the Navir");

        using var check = new Checks(f);
        check.True(f.Has(FlightOfTheNavir.Buff));

        f.WaitUntil(start + 19.9);
        check.True(f.Has(FlightOfTheNavir.Buff));
        f.WaitUntil(start + 20.1);
        check.False(f.Has(FlightOfTheNavir.Buff));
    }

    /// <summary>With Flight of the Navir up, a Cold Snap sends the 5 circling swallows at its target, on top of the 3 its Winter Orb sends.</summary>
    [Fact]
    public void Cold_Snap_With_Flight_Of_The_Navir_Sends_The_5_Circling_Swallows_Too()
    {
        var f = StepFights.Rime();

        f.Cast<FlightOfTheNavir>();

        f.Cast<ColdSnap>();
        f.Wait(3.0); // the swallows land

        Assert.Equal(5 + 3, f.Damage("Frost Swallows").Count);
    }

    /// <summary>
    /// With Flight of the Navir up, every tick of Freezing Torrent, the partial one included, sends one circling swallow, whatever
    /// the torrent: plain, Supreme, Soulfrost or both.
    ///
    /// May fail at high haste once swallows have a travel time: a swallow that is out cannot be sent again.
    /// </summary>
    [Theory]
    [InlineData(false, false, 0.0)]
    [InlineData(false, false, 0.25)]
    [InlineData(false, false, 0.5)]
    [InlineData(false, false, 1.0)]
    [InlineData(true, false, 0.0)]
    [InlineData(true, false, 0.25)]
    [InlineData(true, false, 0.5)]
    [InlineData(true, false, 1.0)]
    [InlineData(false, true, 0.0)]
    [InlineData(false, true, 0.25)]
    [InlineData(false, true, 0.5)]
    [InlineData(false, true, 1.0)]
    [InlineData(true, true, 0.0)]
    [InlineData(true, true, 0.25)]
    [InlineData(true, true, 0.5)]
    [InlineData(true, true, 1.0)]
    public void Every_Torrent_Tick_Sends_A_Circling_Swallow(bool supreme, bool soulfrost, double haste)
    {
        var talents = new List<int>();
        if (supreme) talents.Add(SupremeTorrent.TalentId);
        if (soulfrost) talents.Add(SoulfrostTorrent.TalentId);
        var f = StepFights.Rime(haste: haste, talents: [.. talents]);

        f.Cast<FlightOfTheNavir>();
        if (soulfrost) f.Apply(SoulfrostTorrent.Buff); // the empowered torrent

        f.Cast<FreezingTorrent>();
        f.Wait(2.0); // the swallows land

        int ticks = f.Damage<FreezingTorrent>().Count;
        double orbs = f.Gains("Winter Orbs").Sum(gain => gain.Value); // a long channel's Anima makes orbs, each sending 3 swallows

        Assert.Equal(ticks + 3 * orbs, f.Damage("Frost Swallows").Count);
    }

    // ---- Wrath of Winter (its cost and Spirit of Heroism, shared by every hero's ultimate, are in Generic/Ultimate.cs)

    /// <summary>
    /// Wrath of Winter makes you deal 20% more damage for 20 seconds.
    /// </summary>
    [Fact]
    public void Wrath_Of_Winter_Gives_20_Percent_More_Damage_For_20_Seconds()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>();
        double plainBoltDamage = plainScenario.Damage<FrostBolt>().Single().Value;

        var wrathScenario = StepFights.Rime().WithoutDamageSpread();
        wrathScenario.Set(Hero.SpiritPoints, 100); // enough to cast it

        wrathScenario.Cast<WrathOfWinter>();
        double start = wrathScenario.CompletedAt("Wrath of Winter");

        using var check = new Checks(wrathScenario);
        check.True(wrathScenario.Has(WrathOfWinter.Buff));

        wrathScenario.Cast<FrostBolt>(); // to measure the bonus
        double wrathBoltDamage = wrathScenario.Damage<FrostBolt>().Single().Value;
        check.Ratio(1.2, wrathBoltDamage / plainBoltDamage);

        wrathScenario.WaitUntil(start + 19.9);
        check.True(wrathScenario.Has(WrathOfWinter.Buff));
        wrathScenario.WaitUntil(start + 20.1);
        check.False(wrathScenario.Has(WrathOfWinter.Buff));
    }

    /// <summary>Ice Blitz's and Wrath of Winter's 20% damage bonuses multiply: with both up you deal 1.2 x 1.2 = 1.44 times the damage.</summary>
    [Fact]
    public void Ice_Blitz_And_Wrath_Of_Winter_Damage_Bonuses_Multiply()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>();
        double plainBoltDamage = plainScenario.Damage<FrostBolt>().Single().Value;

        var bothScenario = StepFights.Rime().WithoutDamageSpread();
        bothScenario.Set(Hero.SpiritPoints, 100); // enough for Wrath of Winter

        bothScenario.Cast<IceBlitz>();
        bothScenario.Cast<WrathOfWinter>();
        bothScenario.Cast<FrostBolt>();
        double bothBoltDamage = bothScenario.Damage<FrostBolt>().Single().Value;

        AssertRatio(1.2 * 1.2, bothBoltDamage / plainBoltDamage);
    }

    /// <summary>While Wrath of Winter is up, Glacial Blast is instant, and still costs 2 Winter Orbs.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Glacial_Blast_Is_Instant_And_Costs_2_Orbs_During_Wrath_Of_Winter(double haste)
    {
        var f = StepFights.Rime(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough for Wrath of Winter

        f.Cast<WrathOfWinter>();
        f.RimeOf().WinterOrbs = 3; // replaces the orb Wrath of Winter gives, so the blast's cost shows

        f.Cast<GlacialBlast>();

        using var check = new Checks(f);
        check.Equal(0.0, f.LastCastDuration("Glacial Blast"));
        check.Equal(1, f.RimeOf().WinterOrbs);
    }

    // ---- Bursting Ice

    /// <summary>Bursting Ice puts a debuff on its target and gives you Winter's Embrace, both for 3 seconds.</summary>
    [Fact]
    public void Bursting_Ice_Debuffs_The_Target_And_Gives_Winters_Embrace_For_3_Seconds()
    {
        var f = StepFights.Rime();

        f.Cast<BurstingIce>();
        double start = f.CompletedAt("Bursting Ice");

        using var check = new Checks(f);
        check.True(f.Has(BurstingIce.Debuff, on: f.Enemy));
        check.True(f.Has(BurstingIce.WintersEmbrace));

        f.WaitUntil(start + 2.9);
        check.True(f.Has(BurstingIce.Debuff, on: f.Enemy));
        check.True(f.Has(BurstingIce.WintersEmbrace));
        f.WaitUntil(start + 3.1);
        check.False(f.Has(BurstingIce.Debuff, on: f.Enemy));
        check.False(f.Has(BurstingIce.WintersEmbrace));
    }

    // ---- Frost Swallows

    /// <summary>Every Winter Orb you gain sends 3 Frost Swallows at the enemy, and they hit.</summary>
    [Fact]
    public void Gaining_A_Winter_Orb_Sends_3_Frost_Swallows()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>(); // gives 1 orb
        f.Wait(3.0); // the swallows land

        Assert.Equal(3, f.Damage("Frost Swallows").Count);
    }

    // ---- Area damage reduction: past a number of enemies, each enemy takes the hit times sqrt(threshold / enemies)

    /// <summary>The value of the first hit named <paramref name="hit"/> on each enemy, one per enemy hit.</summary>
    private static List<double> FirstHitOnEachEnemy(StepDriver f, string hit) =>
        f.Damage(hit).GroupBy(h => h.Target).Select(g => g.First().Value).ToList();

    /// <summary>
    /// Bursting Ice's pulses lose damage once they hit more than 5 enemies: each enemy takes the damage times
    /// sqrt(5 / enemies), so the total damage does not grow as fast as the crowd.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(20)]
    public void Bursting_Ice_Pulses_Lose_Damage_Past_5_Enemies(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Rime(enemies: n).WithoutDamageSpread();

            f.Cast<BurstingIce>();
            f.Wait(4.0);
            return f;
        }

        double alone = FirstHitOnEachEnemy(Fight(1), "Bursting Ice").Single();
        var crowd = FirstHitOnEachEnemy(Fight(enemies), "Bursting Ice");

        double expected = enemies <= 5 ? 1.0 : Math.Sqrt(5.0 / enemies);

        using var check = new Checks();
        check.Equal(enemies, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }

    /// <summary>Ice Comet loses damage once it hits more than 12 enemies: each enemy takes the damage times sqrt(12 / enemies).</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(48)]
    public void Ice_Comet_Loses_Damage_Past_12_Enemies(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Rime(enemies: n).WithoutDamageSpread();
            f.RimeOf().WinterOrbs = 2; // enough to cast it

            f.Cast<IceComet>();
            f.Wait(4.0); // the comet falls
            return f;
        }

        double alone = FirstHitOnEachEnemy(Fight(1), "Ice Comet").Single();
        var crowd = FirstHitOnEachEnemy(Fight(enemies), "Ice Comet");

        double expected = enemies <= 12 ? 1.0 : Math.Sqrt(12.0 / enemies);

        using var check = new Checks();
        check.Equal(enemies, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }

    /// <summary>
    /// With Icy Talons, Rising Talons loses damage once it hits more than 12 enemies: each enemy takes the damage times
    /// sqrt(12 / enemies).
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(48)]
    public void Rising_Talons_Loses_Damage_Past_12_Enemies(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Rime(enemies: n, talents: [IcyTalons.TalentId]).WithoutDamageSpread();
            f.RimeOf().WinterOrbs = 1; // one wave

            f.Cast<RisingTalons>();
            f.Wait(5.0); // the wave lands
            return f;
        }

        double alone = FirstHitOnEachEnemy(Fight(1), "Rising Talons").Single();
        var crowd = FirstHitOnEachEnemy(Fight(enemies), "Rising Talons");

        double expected = enemies <= 12 ? 1.0 : Math.Sqrt(12.0 / enemies);

        using var check = new Checks();
        check.Equal(enemies, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }

    /// <summary>
    /// With Glacial Assault, the explosion of a 4-stack Glacial Blast loses damage once it hits more than 12 enemies, not
    /// counting the target the blast strikes: each enemy takes the damage times sqrt(12 / enemies hit). It hits 40 enemies at most.
    ///
    /// From one log (yMAZVJL9hTpFjqbC fight 7: 0.2598 of the spike "on 16"), which does not say whether 16 counts the target. TODO: confirm
    /// in the game data whether the threshold counts the blast's own target.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(36)]
    public void Glacial_Assault_Explosion_Loses_Damage_Past_12_Enemies_Not_Counting_The_Target(int enemiesHit)
    {
        StepDriver Fight(int hit)
        {
            var f = StepFights.Rime(enemies: 1 + hit, talents: [GlacialAssault.TalentId]).WithoutDamageSpread(); // the target, plus the enemies the explosion hits
            f.Apply(GlacialAssault.Buff, stacks: 4);
            f.RimeOf().WinterOrbs = 2; // enough to cast it

            f.Cast<GlacialBlast>();
            f.Wait(3.0); // the explosion lands
            return f;
        }

        double alone = FirstHitOnEachEnemy(Fight(1), "Glacial Assault").Single();
        var crowd = FirstHitOnEachEnemy(Fight(enemiesHit), "Glacial Assault");

        double expected = enemiesHit <= 12 ? 1.0 : Math.Sqrt(12.0 / enemiesHit);

        using var check = new Checks();
        check.Equal(enemiesHit, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }

    /// <summary>
    /// With Coalescing Frost, the eruption of frost stacks hits the stacked enemy in full, and the enemies around it lose damage once
    /// there are more than 5 of them: each takes the damage times sqrt(5 / enemies around).
    ///
    /// From combat logs (10 fights, 300 eruptions: CoalescingFrost.cs); the tooltip suggests every enemy hit counts and loses damage.
    /// TODO: confirm in the game data that the stacked enemy is left out of the count and of the reduction.
    /// </summary>
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(20)]
    public void Coalescing_Frost_Eruption_Hits_Its_Enemy_In_Full_And_The_Others_Lose_Damage_Past_5(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Rime(enemies: n, talents: [CoalescingFrost.TalentId]).WithoutDamageSpread();

            f.Cast<FreezingTorrent>(); // stacks the first enemy
            f.Wait(6.0); // the eruption comes 3s after the last stack
            return f;
        }

        double alone = FirstHitOnEachEnemy(Fight(1), "Coalescing Frost").Single();
        var crowd = Fight(enemies);
        string? stacked = crowd.Damage<FreezingTorrent>()[0].Target;
        var eruption = crowd.Damage("Coalescing Frost").GroupBy(h => h.Target).Select(g => g.First()).ToList();

        int around = enemies - 1;
        double expectedAround = around <= 5 ? 1.0 : Math.Sqrt(5.0 / around);

        using var check = new Checks();
        check.Equal(enemies, eruption.Count, what: "enemies hit");
        foreach (var hit in eruption)
            check.Ratio(hit.Target == stacked ? 1.0 : expectedAround, hit.Value / alone, what: hit.Target == stacked ? "the stacked enemy" : hit.Target);
    }
}
