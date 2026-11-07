using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Spirit Points every hero regains on their own in combat. Each test runs over every hero.
///
/// What spends them is in Generic/Ultimate.cs, and what a Spirit Refund gives in Generic/SpiritRefund.cs.
/// </summary>
public class SpiritPoints
{
    /// <summary>
    /// You regain 1 Spirit Point every 3 seconds in combat, whatever your Spirit: the stat raises the chance of a Spirit Refund, not
    /// this.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0)]
    [InlineData("Rime", 0.3)]
    [InlineData("Ardeos", 0.0)]
    [InlineData("Ardeos", 0.3)]
    [InlineData("Elarion", 0.0)]
    [InlineData("Elarion", 0.3)]
    [InlineData("Gunde", 0.0)]
    [InlineData("Gunde", 0.3)]
    [InlineData("Mara", 0.0)]
    [InlineData("Mara", 0.3)]
    [InlineData("Tariq", 0.0)]
    [InlineData("Tariq", 0.3)]
    public void Spirit_Points_Come_Back_1_Every_3_Seconds_Whatever_The_Spirit(string hero, double spirit)
    {
        var f = StepFights.Of(hero, spirit: spirit);
        f.Set(Hero.SpiritPoints, 50); // room to regain

        using var check = new Checks(f);
        f.WaitUntil(3 - 0.1);
        check.Equal(50, f.Amount(Hero.SpiritPoints));
        f.WaitUntil(3 + 0.1);
        check.Equal(51, f.Amount(Hero.SpiritPoints));
        f.WaitUntil(12 + 0.1);
        check.Equal(54, f.Amount(Hero.SpiritPoints));
    }

    /// <summary>The Spirit Points you regain stop at 100.</summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Ardeos")]
    [InlineData("Elarion")]
    [InlineData("Gunde")]
    [InlineData("Mara")]
    [InlineData("Tariq")]
    public void Regained_Spirit_Points_Stop_At_100(string hero)
    {
        var f = StepFights.Of(hero);
        f.Set(Hero.SpiritPoints, 100);

        f.Wait(7); // two regenerations

        Assert.Equal(100, f.Amount(Hero.SpiritPoints));
    }
}
