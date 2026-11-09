using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Gunde's cooldowns last: the base durations and charges, and what haste does to them (Reaver's Edge, Blood Arc, Heart
/// Splitter, Grim Carve and the auto attack are shortened by it, at the cast). The talents that shorten them are in GundeTalents.cs.
///
/// Every duration is checked by the ability being not ready 0.1s before its end and ready 0.1s after.
/// </summary>
public class GundeCooldowns
{
    // ---- Base durations and haste

    /// <summary>
    /// Reaver's Edge has a 5 second cooldown, Blood Arc 9, Heart Splitter 12 and Grim Carve 15, and haste shortens them:
    /// base / (1 + haste).
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
    public void Hasted_Cooldowns_Follow_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Gunde(haste: haste);

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown / (1 + haste);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    /// <summary>
    /// The cooldowns of Rupture (60 seconds), Slaughter (30), Reign in Blood (90), Owed in Blood (3), Butcher's Hook (30), Jawbreaker (16)
    /// and Reckless Abandon (30) are not shortened by haste.
    ///
    /// Jawbreaker's 16 seconds is game data (ability 1244); no logged interval is shorter.
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
    [InlineData("Jawbreaker", 16, 0.0)]
    [InlineData("Jawbreaker", 16, 0.5)]
    [InlineData("Reckless Abandon", 30, 0.0)]
    [InlineData("Reckless Abandon", 30, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Gunde(haste: haste);
        f.Apply(OwedInBlood.Buff, stacks: 1); // enough to cast Owed in Blood

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Warbound

    /// <summary>
    /// Warbound has 2 charges of 8 seconds, and haste does not shorten them: two casts in a row, a third refused, and the first charge
    /// back 8 seconds after the first cast, at 50% haste.
    /// </summary>
    [Fact]
    public void Warbound_Has_2_Charges_Of_8_Seconds_Ignoring_Haste()
    {
        var f = StepFights.Gunde(haste: 0.5);
        Assert.Equal(2, f.Charges<Warbound>());

        f.Cast<Warbound>();
        double end = f.CompletedAt("Warbound") + 8.0;
        f.Cast<Warbound>();

        using var check = new Checks(f);
        check.False(f.TryCast<Warbound>().Succeeded);
        f.WaitUntil(end - 0.1);
        check.False(f.IsReady<Warbound>());
        f.WaitUntil(end + 0.1);
        check.True(f.IsReady<Warbound>());
    }

    // ---- Attack

    /// <summary>Your auto attack swings every 1.5 seconds, shortened by haste: 1.5 / (1 + haste).</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Auto_Attack_Swings_Every_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste);

        f.Cast<Attack>();
        double end = f.CompletedAt("Attack") + 1.5 / (1 + haste);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<Attack>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<Attack>());
    }
}
