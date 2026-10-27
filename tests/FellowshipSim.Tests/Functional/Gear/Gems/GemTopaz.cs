using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Topaz gem on every hero: Adrenaline Rush (+3% / +9% haste for 10s when you damage an enemy at 30% health or less), Feline Grace (+2% / +6% haste),
/// Blessing of the Virtuoso (+2% / +6% haste, but not while Spirit of Heroism is active); Thief's Alacrity adds haste rating and Rogue's Resurgence heals.
/// Dev: no character sheet. Adrenaline Rush is read from the hero's haste right after a hit, against the same hit on an enemy at 31% health.
/// </summary>
public class GemTopaz
{
    private const GemColor Color = GemColor.Topaz;

    /// <summary>The hero's haste after one basic hit on an enemy at <paramref name="health"/> of its health.</summary>
    private static (StepDriver Fight, double Haste) AfterHit(GearHero hero, double power, double health)
    {
        var f = GemFights.Fight(hero, Color, power);
        f.Enemy.CurrentHealth = f.Enemy.MaxHealth * health;
        hero.CastProbe(f, hero.Basic);
        return (f, f.Haste());
    }

    /// <summary>
    /// Player: dealing damage to an enemy at 30% health or less gives +3% haste (+9% at rank II); against a healthier enemy it does not.
    /// Dev: haste after a hit on an enemy at 29% health minus the haste after the same hit at 31% health, at 80 and at 600 gem power (the other
    /// nodes are the same in both). The exact 30% is not tested.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Adrenaline_Rush_Gives_Haste_Against_Weak_Enemies(GearHero hero)
    {
        Assert.Equal(0.03, AfterHit(hero, 80, 0.29).Haste - AfterHit(hero, 80, 0.31).Haste, 1e-9);
        Assert.Equal(0.09, AfterHit(hero, 600, 0.29).Haste - AfterHit(hero, 600, 0.31).Haste, 1e-9);
    }

    /// <summary>
    /// Player: Adrenaline Rush lasts 10 seconds from the last hit that gave it.
    /// Dev: the aura's removal comes 10.0s after the last damage the hero dealt before it (tolerance 20ms: an aura ends on the server's next frame, 1/60s).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Adrenaline_Rush_Lasts_Ten_Seconds(GearHero hero)
    {
        var (f, _) = AfterHit(hero, 80, 0.29);
        f.Wait(40);
        var removed = f.Auras(StepEventKind.AuraRemoved, "Adrenaline Rush").First();
        var lastHit = f.Events.Where(e => e.Kind == StepEventKind.Damage && e.Time <= removed.Time).Max(e => e.Time);
        Assert.Equal(10.0, (removed.Time - lastHit).TotalSeconds, 0.02);
    }

    /// <summary>
    /// Player: Feline Grace gives +2% haste (+6% at rank II).
    /// Dev: haste at 299 / 300 gem power (+0.02) and 1249 / 1250 (rank I to rank II: +0.04).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Feline_Grace_Adds_Haste(GearHero hero)
    {
        Assert.Equal(0.02, GemFights.Delta(hero, Color, 299, 300, GemFights.Haste), 1e-9);
        Assert.Equal(0.04, GemFights.Delta(hero, Color, 1249, 1250, GemFights.Haste), 1e-9);
    }

    /// <summary>
    /// Player: Blessing of the Virtuoso gives +2% haste (+6% at rank II) while Spirit of Heroism is not active, and gives nothing during it:
    /// you keep the haste Spirit of Heroism gives while it is gone.
    /// Dev: haste at 449 / 450 gem power: +0.02 at the start, +0 right after the Spirit ability is cast, +0.02 again once Spirit of Heroism ended;
    /// the same at 1499 / 1500 with +0.04.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Virtuoso_Is_Up_Only_Outside_Spirit_Of_Heroism(GearHero hero)
    {
        foreach (var (low, high, bonus) in new[] { (449.0, 450.0, 0.02), (1499.0, 1500.0, 0.04) })
        {
            var a = GemFights.Fight(hero, Color, low);
            var b = GemFights.Fight(hero, Color, high);
            Assert.Equal(bonus, b.Haste() - a.Haste(), 1e-9);
            a.StartHeroism(hero);
            b.StartHeroism(hero);
            Assert.Equal(0.0, b.Haste() - a.Haste(), 1e-9);
            a.Wait(45);
            b.Wait(45);
            Assert.Equal(bonus, b.Haste() - a.Haste(), 1e-9);
        }
    }

    /// <summary>
    /// Player: Rogue's Resurgence (healing) does not change your damage.
    /// Dev: the unit hit is the same at 219 / 220 and 999 / 1000 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Rogues_Resurgence_Does_Not_Change_Damage(GearHero hero)
    {
        Assert.Equal(GemFights.UnitHit(hero, Color, 219), GemFights.UnitHit(hero, Color, 220), 1e-9);
        Assert.Equal(GemFights.UnitHit(hero, Color, 999), GemFights.UnitHit(hero, Color, 1000), 1e-9);
    }

    /// <summary>
    /// Player: Thief's Alacrity gives 8 haste rating (24 at rank II).
    /// Dev: a hero built from gem power (no character sheet, so nothing else gives rating): the haste at the node's power over the one just below is the
    /// percentage of 8 rating (rank II: of 24 less 8, as it replaces the rank I node), through the stat's diminishing returns.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Thiefs_Alacrity_Adds_Haste_Rating(GearHero hero)
    {
        Assert.Equal(StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 149, 150, GemFights.Haste), 1e-9);
        Assert.Equal(StatDiminishingReturns.FromRating(24) - StatDiminishingReturns.FromRating(8), GemFights.Delta(hero, Color, 799, 800, GemFights.Haste), 1e-9);
    }
}
