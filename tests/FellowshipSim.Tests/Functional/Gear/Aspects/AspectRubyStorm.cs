using FellowshipSim.Core.Customization.Aspects;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ruby Storm (aspect 35): "Your targeted, offensive abilities have a chance to spawn a Ruby Storm that travels forward, dealing damage to
/// all enemies it touches equal to 2.4% / 4.0% / 5.4% / 7.0% of your Current Health" (ranks 1 to 4). The chance is forced.
///
/// The sim keeps the hero at full health, so Current Health is the hero's health. The storm touches every enemy in combat.
/// </summary>
public class AspectRubyStorm
{
    private const string Storm = "Ruby Storm";

    /// <summary>A hero with Ruby Storm at <paramref name="rank"/>.</summary>
    private static StepDriver Fight(string hero, int rank, double crit = 0, double mainStat = 0, double expertise = 0, int enemies = 1)
    {
        return StepFights.Of(hero, crit: crit, mainStat: mainStat, expertise: expertise, enemies: enemies,
            augmentations: [new(RubyStorm.AspectId, rank)]).WithoutDamageSpread();
    }

    /// <summary>A Ruby Storm deals 2.4% / 4.0% / 5.4% / 7.0% of your health.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.024)]
    [InlineData("Rime", "Frost Bolt", 2, 0.040)]
    [InlineData("Rime", "Frost Bolt", 3, 0.054)]
    [InlineData("Rime", "Frost Bolt", 4, 0.070)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.070)]
    [InlineData("Elarion", "Focused Shot", 4, 0.070)]
    [InlineData("Gunde", "Double Strike", 4, 0.070)]
    [InlineData("Mara", "Skittering Blades", 4, 0.070)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.070)]
    public void A_Ruby_Storm_Deals_A_Share_Of_Your_Health(string hero, string ability, int rank, double share)
    {
        var f = Fight(hero, rank);
        f.ForceProcs(key => key == RubyStorm.ProcKey ? true : null);

        f.Cast(ability);
        f.Wait(1); // the storm lands

        AssertRatio(share, f.Damage(Storm)[0].Value / f.Health());
    }

    /// <summary>A Ruby Storm scales with nothing but your health: not with main stat or Expertise, and it never crits.</summary>
    [Fact]
    public void A_Ruby_Storm_Scales_With_Nothing_But_Your_Health()
    {
        var f = Fight("Rime", rank: 4, crit: 1.0, mainStat: 5000, expertise: 0.5);
        f.ForceProcs(key => key == RubyStorm.ProcKey ? true : null);

        f.Cast("Frost Bolt");
        f.Wait(1); // the storm lands

        var storm = f.Damage(Storm)[0];

        using var check = new Checks(f);
        check.Ratio(0.07, storm.Value / f.Health());
        check.Equal(0, storm.CritChance);
    }

    /// <summary>A cast rolls for a Ruby Storm once, however many hits it makes: Bursting Ice and its pulses roll once.</summary>
    [Fact]
    public void A_Cast_Rolls_Once_For_A_Ruby_Storm()
    {
        var f = Fight("Rime", rank: 1);
        var procs = f.ForceProcs(key => key == RubyStorm.ProcKey ? false : null); // only to count the rolls

        f.Cast("Bursting Ice");
        f.Wait(4); // the pulses

        Assert.Single(procs.ChancesOf(RubyStorm.ProcKey));
    }

    /// <summary>A Ruby Storm hits every enemy, each for the whole share.</summary>
    [Fact]
    public void A_Ruby_Storm_Hits_Every_Enemy()
    {
        var f = Fight("Rime", rank: 4, enemies: 3);
        f.ForceProcs(key => key == RubyStorm.ProcKey ? true : null);

        f.Cast("Frost Bolt");
        f.Wait(1); // the storm lands

        var storms = f.FirstHitOnEachEnemy(Storm);

        using var check = new Checks(f);
        check.Equal(3, storms.Count);
        foreach (double storm in storms) check.Ratio(0.07, storm / f.Health());
    }
}
