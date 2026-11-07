using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// What every hero's ultimate (the game's Spirit ability) shares, whatever its own effect: its Spirit cost, Spirit of Heroism, no
/// cooldown. Each test runs over every hero.
///
/// Each ultimate's own effect is in its hero's Abilities file.
/// </summary>
public class Ultimate
{
    /// <summary>Your ultimate costs 100 Spirit: with 99 you cannot cast it, and casting it uses up all 100.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter")]
    [InlineData("Ardeos", "Incinerate")]
    [InlineData("Elarion", "Event Horizon")]
    [InlineData("Gunde", "Bloodbound Spirit")]
    [InlineData("Mara", "Matriarch Macabre")]
    [InlineData("Tariq", "Raging Tempest")]
    public void The_Ultimate_Costs_100_Spirit_Points(string hero, string ultimate)
    {
        var f = StepFights.Of(hero);

        f.Set(Hero.SpiritPoints, 99);

        using var check = new Checks(f);
        check.False(f.TryCast(ultimate).Succeeded);

        f.Set(Hero.SpiritPoints, 100);
        f.Cast(ultimate);
        check.Equal(f.Gains("Spirit").Sum(gain => gain.Value), f.Amount(Hero.SpiritPoints)); // only what came back during the cast is left
    }

    /// <summary>Casting your ultimate gives you Spirit of Heroism for 20 seconds: 30% haste, added to your own.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter", 0.0)]
    [InlineData("Rime", "Wrath of Winter", 0.2)]
    [InlineData("Rime", "Wrath of Winter", 0.45)]
    [InlineData("Ardeos", "Incinerate", 0.0)]
    [InlineData("Ardeos", "Incinerate", 0.2)]
    [InlineData("Ardeos", "Incinerate", 0.45)]
    [InlineData("Elarion", "Event Horizon", 0.0)]
    [InlineData("Elarion", "Event Horizon", 0.2)]
    [InlineData("Elarion", "Event Horizon", 0.45)]
    [InlineData("Gunde", "Bloodbound Spirit", 0.0)]
    [InlineData("Gunde", "Bloodbound Spirit", 0.2)]
    [InlineData("Gunde", "Bloodbound Spirit", 0.45)]
    [InlineData("Mara", "Matriarch Macabre", 0.0)]
    [InlineData("Mara", "Matriarch Macabre", 0.2)]
    [InlineData("Mara", "Matriarch Macabre", 0.45)]
    [InlineData("Tariq", "Raging Tempest", 0.0)]
    [InlineData("Tariq", "Raging Tempest", 0.2)]
    [InlineData("Tariq", "Raging Tempest", 0.45)]
    public void The_Ultimate_Gives_Spirit_Of_Heroism_Adding_30_Percent_Haste_For_20_Seconds(string hero, string ultimate, double haste)
    {
        var f = StepFights.Of(hero, haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        using var check = new Checks(f);
        check.Equal(haste, f.Haste());

        f.Cast(ultimate);
        double start = f.CompletedAt(ultimate);
        check.True(f.Has(Hero.SpiritOfHeroism));
        check.Equal(haste + 0.3, f.Haste());

        f.WaitUntil(start + 19.9);
        check.True(f.Has(Hero.SpiritOfHeroism));
        check.Equal(haste + 0.3, f.Haste());
        f.WaitUntil(start + 20.1);
        check.False(f.Has(Hero.SpiritOfHeroism));
        check.Equal(haste, f.Haste());
    }

    /// <summary>Your ultimate has no cooldown: with 100 Spirit again, you can cast it again right away.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter")]
    [InlineData("Ardeos", "Incinerate")]
    [InlineData("Elarion", "Event Horizon")]
    [InlineData("Gunde", "Bloodbound Spirit")]
    [InlineData("Mara", "Matriarch Macabre")]
    [InlineData("Tariq", "Raging Tempest")]
    public void The_Ultimate_Has_No_Cooldown(string hero, string ultimate)
    {
        var f = StepFights.Of(hero);
        f.Set(Hero.SpiritPoints, 100);

        f.Cast(ultimate);

        f.Set(Hero.SpiritPoints, 100);

        Assert.True(f.TryCast(ultimate).Succeeded);
    }

    /// <summary>Casting your ultimate again during Spirit of Heroism restarts its 20 seconds.</summary>
    [Theory]
    [InlineData("Rime", "Wrath of Winter")]
    [InlineData("Ardeos", "Incinerate")]
    [InlineData("Elarion", "Event Horizon")]
    [InlineData("Gunde", "Bloodbound Spirit")]
    [InlineData("Mara", "Matriarch Macabre")]
    [InlineData("Tariq", "Raging Tempest")]
    public void Recasting_During_Spirit_Of_Heroism_Restarts_Its_20_Seconds(string hero, string ultimate)
    {
        var f = StepFights.Of(hero);
        f.Set(Hero.SpiritPoints, 100);

        f.Cast(ultimate);
        double first = f.CompletedAt(ultimate);

        f.WaitUntil(first + 10);
        f.Set(Hero.SpiritPoints, 100);
        f.Cast(ultimate);
        double second = f.CompletedAt(ultimate);

        using var check = new Checks(f);
        f.WaitUntil(second + 19.9);
        check.True(f.Has(Hero.SpiritOfHeroism));
        f.WaitUntil(second + 20.1);
        check.False(f.Has(Hero.SpiritOfHeroism));
    }
}
