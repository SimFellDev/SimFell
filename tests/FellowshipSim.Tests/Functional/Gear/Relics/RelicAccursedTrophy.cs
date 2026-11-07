using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Accursed Trophy, a relic's passive. Worn by a bare hero. The effect runs over every hero, since a relic works through what the hero
/// casts, not through named abilities; its details run on Rime. Every resource is filled before a cast, so any ability can be cast. Procs
/// are forced.
/// </summary>
public class RelicAccursedTrophy
{
    /// <summary>With Accursed Trophy, your ultimate gives you Drakheim's Absolution: 20% more main stat for 20 seconds.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter")]
    [InlineData("Ardeos", "Incinerate")]
    [InlineData("Elarion", "Event Horizon")]
    [InlineData("Gunde", "Bloodbound Spirit")]
    [InlineData("Mara", "Matriarch Macabre")]
    [InlineData("Tariq", "Raging Tempest")]
    public void Accursed_Trophy_Ultimate_Gives_20_Percent_Main_Stat_For_20_Seconds(string hero, string ultimate)
    {
        var f = StepFights.Of(hero, relics: [AccursedTrophy.OrbItemId]);
        double plainMainStat = f.MainStat();
        f.FillResources(); // enough to cast it

        f.Cast(ultimate);
        double start = f.CompletedAt(ultimate);

        using var check = new Checks(f);
        check.Ratio(1.2, f.MainStat() / plainMainStat);
        f.WaitUntil(start + 20 - 0.1);
        check.True(f.Has(AccursedTrophy.Buff));
        f.WaitUntil(start + 20 + 0.1);
        check.False(f.Has(AccursedTrophy.Buff));
    }

    /// <summary>
    /// A second ultimate while Drakheim's Absolution is up gives it a second stack.
    ///
    /// Per the user: rare, since it takes 100 Spirit Points twice within 20 seconds, but it happens in normal play.
    /// </summary>
    [Fact]
    public void Accursed_Trophy_A_Second_Ultimate_Gives_A_Second_Stack_Of_Drakheims_Absolution()
    {
        var f = StepFights.Of("Rime", relics: [AccursedTrophy.OrbItemId]);
        f.FillResources(); // enough to cast it

        f.Cast<WrathOfWinter>();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it again

        f.Cast<WrathOfWinter>();

        Assert.Equal(2, f.Stacks(AccursedTrophy.Buff));
    }

    /// <summary>Only your ultimate gives Drakheim's Absolution: your other abilities do not.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Bursting Ice")]
    [InlineData("Ice Comet")]
    [InlineData("Ice Blitz")]
    public void Only_The_Ultimate_Gives_Drakheims_Absolution(string ability)
    {
        var f = StepFights.Of("Rime", relics: [AccursedTrophy.OrbItemId]);
        f.FillResources(); // enough to cast it

        f.Cast(ability);

        Assert.False(f.Has(AccursedTrophy.Buff));
    }
}
