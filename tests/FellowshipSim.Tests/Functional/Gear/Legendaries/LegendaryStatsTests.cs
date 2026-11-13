using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The stats every legendary item carries, whatever its power: Strand of Eternity (+10% cooldown recovery, effect 3087), Mote of Creation
/// (+10% main stat, effect 3085) and Blood of the Undying (+10% health, effect 3366). The game data lists all three on each legendary
/// item, the head (5899), the chest (5898) and the legs (5900), so whichever one a hero wears gives all three. The effects run over every
/// hero; their details run on Rime.
///
/// A hero can wear only one legendary at a time in the current game (per the user), so the stats never stack.
///
/// Each hero wears a legendary whose power does nothing in these fights (<see cref="InertLegendary"/>), so only its stats show.
/// </summary>
public class LegendaryStatsTests
{
    /// <summary>A legendary item of <paramref name="hero"/>'s whose power the bare hero never sets off in these tests.</summary>
    private static int InertLegendary(string hero) => hero switch
    {
        "Rime" => UndulatingSpirit.ChestItemId, // works through Winter's Blessing
        "Ardeos" => Explosivo.HelmItemId, // works through Apocalypse and Fire Ball
        "Elarion" => StarstrikersAscent.HauberkItemId, // works through a Spirit Refund, and the bare hero has no Spirit
        "Gunde" => BloodyMess.HelmItemId, // works through Grim Carve
        "Mara" => DrenchedInBlood.ChestItemId, // works through a Spirit Refund, and the bare hero has no Spirit
        "Tariq" => SlayersMosh.HelmItemId, // works through Leap Smash
        _ => throw new ArgumentException(hero),
    };

    // ---- Strand of Eternity

    /// <summary>
    /// Wearing a legendary item, your cooldowns recover 10% faster (Strand of Eternity): a cooldown of c seconds is ready c / 1.1 seconds
    /// after the cast.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz", 60.0)]
    [InlineData("Ardeos", "Fire Frogs", 45.0)]
    [InlineData("Elarion", "Starfall Volley", 40.0)]
    [InlineData("Gunde", "Rupture", 60.0)]
    [InlineData("Mara", "Maiden of Death", 60.0)]
    [InlineData("Tariq", "Thunder Call", 45.0)]
    public void Strand_Of_Eternity_Makes_Cooldowns_Recover_10_Percent_Faster(string hero, string ability, double cooldown)
    {
        var f = StepFights.Of(hero, legendaries: [InertLegendary(hero)]);
        f.FillResources(); // enough to cast it

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown / 1.1;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Mote of Creation

    /// <summary>Wearing a legendary item gives you 10% more main stat (Mote of Creation).</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Mote_Of_Creation_Gives_10_Percent_Main_Stat(string hero)
    {
        var plainScenario = StepFights.Of(hero);
        var legendaryScenario = StepFights.Of(hero, legendaries: [InertLegendary(hero)]);

        AssertRatio(1.1, legendaryScenario.MainStat() / plainScenario.MainStat());
    }

    /// <summary>Wearing a legendary item, your Frost Bolt deals 10% more damage, from Mote of Creation's 10% more main stat.</summary>
    [Fact]
    public void Mote_Of_Creation_Makes_A_Hit_Deal_10_Percent_More()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast("Frost Bolt");
        plainScenario.Wait(1); // the bolt lands
        double plainBoltDamage = plainScenario.Damage("Frost Bolt").Single().Value;

        var legendaryScenario = StepFights.Rime(legendaries: [InertLegendary("Rime")]).WithoutDamageSpread();

        legendaryScenario.Cast("Frost Bolt");
        legendaryScenario.Wait(1); // the bolt lands
        double legendaryBoltDamage = legendaryScenario.Damage("Frost Bolt").Single().Value;

        AssertRatio(1.1, legendaryBoltDamage / plainBoltDamage);
    }

    /// <summary>
    /// Mote of Creation's 10% adds to Rising Spirit's 5% per stack rather than multiplying it: with a legendary item and 5 stacks, your
    /// main stat is 1 + 0.1 + 0.25 times the bare hero's.
    /// </summary>
    [Fact]
    public void Mote_Of_Creation_Adds_To_Rising_Spirits_Bonus()
    {
        var plainScenario = StepFights.Rime();
        var legendaryScenario = StepFights.Rime(legendaries: [InertLegendary("Rime")]);

        legendaryScenario.Apply(Hero.RisingSpirit, stacks: 5); // five Spirit Refunds

        AssertRatio(1 + 0.1 + 0.05 * 5, legendaryScenario.MainStat() / plainScenario.MainStat());
    }

    // ---- Blood of the Undying

    /// <summary>Wearing a legendary item gives you 10% more health (Blood of the Undying).</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Blood_Of_The_Undying_Gives_10_Percent_Health(string hero)
    {
        var plainScenario = StepFights.Of(hero);
        var legendaryScenario = StepFights.Of(hero, legendaries: [InertLegendary(hero)]);

        AssertRatio(1.1, legendaryScenario.Health() / plainScenario.Health());
    }
}
