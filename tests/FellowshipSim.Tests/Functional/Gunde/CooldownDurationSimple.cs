using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's base cooldowns and charges, and how haste affects them (Reaver's Edge, Blood Arc, Heart Splitter, Grim Carve and the auto attack are
/// shortened by it, at the cast). Talents that shorten them are in the talent files.
/// Dev: the time an ability is ready again is read from the hero's cooldown state right after the cast at the start of a fight (now + time remaining),
/// within 2ms.
/// </summary>
public class GundeCooldownDurationSimple
{
    private const double Tolerance = 0.002;

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    private static Ability Named(StepDriver f, string name) => f.Hero.Abilities.First(a => a.Name == name);

    /// <summary>
    /// Player: Reaver's Edge has a 5 second cooldown, Blood Arc 9, Heart Splitter 12 and Grim Carve 15, and haste shortens them: base / (1 + haste).
    /// Dev: at 0% and 20% haste; one cast at the start of the fight; the ability is ready that long after it.
    /// </summary>
    [Theory]
    [InlineData("Reaver's Edge", 5, 0.0)]
    [InlineData("Reaver's Edge", 5, 0.2)]
    [InlineData("Blood Arc", 9, 0.0)]
    [InlineData("Blood Arc", 9, 0.2)]
    [InlineData("Heart Splitter", 12, 0.0)]
    [InlineData("Heart Splitter", 12, 0.2)]
    [InlineData("Grim Carve", 15, 0.0)]
    [InlineData("Grim Carve", 15, 0.2)]
    public void Hasted_Cooldowns_Follow_Haste(string name, double seconds, double haste)
    {
        var f = StepFights.Gunde(haste);
        var ability = Named(f, name);

        f.Cast(ability);

        Assert.Equal(seconds / (1 + haste), ReadyAt(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: the cooldowns of Rupture (60 seconds), Slaughter (30), Reign in Blood (90), Owed in Blood (3), Butcher's Hook (30), Jawbreaker (12)
    /// and Reckless Abandon (30) are not shortened by haste.
    /// Dev: at 0% and 50% haste; one cast at the start of the fight (a stack of Owed in Blood given for it); the ability is ready that long after it.
    /// </summary>
    [Theory]
    [InlineData("Rupture", 60, 0.0)]
    [InlineData("Rupture", 60, 0.5)]
    [InlineData("Slaughter", 30, 0.0)]
    [InlineData("Slaughter", 30, 0.5)]
    [InlineData("Reign in Blood", 90, 0.0)]
    [InlineData("Reign in Blood", 90, 0.5)]
    [InlineData("Owed in Blood", 3, 0.0)]
    [InlineData("Owed in Blood", 3, 0.5)]
    [InlineData("Butcher's Hook", 30, 0.0)]
    [InlineData("Butcher's Hook", 30, 0.5)]
    [InlineData("Jawbreaker", 12, 0.0)]
    [InlineData("Jawbreaker", 12, 0.5)]
    [InlineData("Reckless Abandon", 30, 0.0)]
    [InlineData("Reckless Abandon", 30, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string name, double seconds, double haste)
    {
        var f = StepFights.Gunde(haste);
        f.SetOwedInBlood(1);
        var ability = Named(f, name);

        f.Cast(ability);

        Assert.Equal(seconds, ReadyAt(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: Warbound has 2 charges of 8 seconds, and haste does not shorten them.
    /// Dev: at 50% haste; two casts in a row, both allowed, a third refused; the first charge is back 8 seconds after the first cast.
    /// </summary>
    [Fact]
    public void Warbound_Has_2_Charges_Of_8_Seconds_Ignoring_Haste()
    {
        var f = StepFights.Gunde(0.5);
        var ability = f.Ability<Warbound>();
        Assert.Equal(2, f.Hero.CooldownOf(ability).MaxCharges);

        f.Cast<Warbound>();
        double first = f.Now.TotalSeconds;
        f.Cast<Warbound>();

        Assert.False(f.TryCast<Warbound>().Succeeded);
        Assert.Equal(first + 8.0, ReadyAt(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: your auto attack swings every 1.5 seconds, shortened by haste.
    /// Dev: at 0% and 20% haste; one swing; the next one is ready 1.5 / (1 + haste) seconds after it.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Auto_Attack_Swings_Every_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Gunde(haste);
        var ability = f.Ability<Attack>();

        f.Cast<Attack>();

        Assert.Equal(1.5 / (1 + haste), ReadyAt(f, ability), Tolerance);
    }
}
