using FellowshipSim.Core.Customization.RelicPassives;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Undying Heart, a relic's passive. Worn by a bare hero. The effect runs over every hero, since a relic works through what the hero casts,
/// not through named abilities; its details run on Rime. Every resource is filled before a cast, so any ability can be cast. Procs are
/// forced.
/// </summary>
public class RelicUndyingHeart
{
    /// <summary>With Undying Heart, your abilities can give you Dark Prophecy: 25% haste for 20 seconds.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Skittering Blades")]
    [InlineData("Tariq", "Heavy Strike")]
    public void Undying_Heart_Gives_25_Percent_Haste_For_20_Seconds(string hero, string ability)
    {
        var f = StepFights.Of(hero, relics: [UndyingHeart.OrbItemId]);
        f.ForceProcs(key => key == UndyingHeart.ProcKey ? true : null);
        f.FillResources(); // enough to cast it

        f.Cast(ability);
        double start = f.CompletedAt(ability);

        using var check = new Checks(f);
        check.Equal(0.25, f.Haste());
        f.WaitUntil(start + 20 - 0.1);
        check.True(f.Has(UndyingHeart.Buff));
        f.WaitUntil(start + 20 + 0.1);
        check.False(f.Has(UndyingHeart.Buff));
    }
}
