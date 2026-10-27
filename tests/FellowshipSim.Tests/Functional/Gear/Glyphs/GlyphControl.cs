using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// Glyphs and Control abilities. Gunde's Butcher's Hook is a Control ability that deals damage (37% of Strength, 30 second cooldown, cast during the global cooldown),
/// so no glyph that works on a category (Core, Power, Major, Basic) reaches it, while the damage-to-low-health glyph (The Subduer) does.
/// Dev: Gunde's hook probe, with and without the glyph at rank 4; its first hit's crit chance and damage, and what the glyph's other effects do.
/// (Rime's Frigid Winds, the other Control ability, deals no damage and has nothing to compare.)
/// </summary>
public class GlyphControlTests
{
    private const double Tolerance = 1e-9;

    private static (StepEvent Hit, StepDriver Fight) Hook(int glyph, int rank, double crit = 0.2, double spirit = 0.0, double haste = 0.0)
    {
        var f = Fight("Gunde", glyph, rank, crit: crit, spirit: spirit, haste: haste);
        return (Hit("Gunde", f, Category.Control)!.Value, f);
    }

    /// <summary>
    /// Player: The Sinister does not add crit chance or crit power to Butcher's Hook (it is a Control ability, not a Core one).
    /// Dev: 20% crit; the hook's hit has the same chance and damage with the glyph.
    /// </summary>
    [Fact]
    public void The_Sinister_Does_Not_Touch_The_Hook()
    {
        var (without, _) = Hook(0, 1);
        var (with, _) = Hook(Sinister, 4);
        Assert.Equal(0.2, without.CritChance, Tolerance);
        Assert.Equal(without.CritChance, with.CritChance, Tolerance);
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    /// <summary>
    /// Player: casting Butcher's Hook does not grant The Trickster's crit chance (it is not a Power ability).
    /// Dev: after the hook the hero's crit chance is the sheet's 20%.
    /// </summary>
    [Fact]
    public void The_Trickster_Is_Not_Granted_By_The_Hook()
    {
        var (_, f) = Hook(Trickster, 4);
        Assert.Equal(0.2, f.Hero.CritChance(f.Now), Tolerance);
    }

    /// <summary>
    /// Player: The Mystic does not add power to Butcher's Hook.
    /// Dev: 50% Spirit, rank 4; the hook's damage is the same with and without the glyph.
    /// </summary>
    [Fact]
    public void The_Mystic_Does_Not_Touch_The_Hook()
    {
        var (without, _) = Hook(0, 1, spirit: 0.5);
        var (with, _) = Hook(Mystic, 4, spirit: 0.5);
        Assert.Equal(without.Value, with.Value, 1e-6);
    }

    /// <summary>
    /// Player: The Monarch does not speed up Butcher's Hook's cooldown (it is not a Major ability), and The Philosopher grants nothing for it.
    /// Dev: the hook's cooldown length with and without The Monarch (rank 4, 20% haste), and the haste after the hook with The Philosopher (rank 4, 40% Spirit).
    /// </summary>
    [Fact]
    public void The_Monarch_And_The_Philosopher_Do_Not_Touch_The_Hook()
    {
        var plain = Fight("Gunde", haste: 0.2);
        var monarch = Fight("Gunde", Monarch, 4, haste: 0.2);
        Assert.Equal(CooldownLength(plain, Hero("Gunde").Get(plain, typeof(ButchersHook))),
            CooldownLength(monarch, Hero("Gunde").Get(monarch, typeof(ButchersHook))), 1e-3);

        var without = Fight("Gunde", spirit: 0.4);
        var with = Fight("Gunde", Philosopher, 4, spirit: 0.4);
        foreach (var f in new[] { without, with }) f.Cast<ButchersHook>();
        Assert.Equal(without.Hero.GetHaste(without.Now), with.Hero.GetHaste(with.Now), Tolerance);
    }

    /// <summary>
    /// Player: The Wayfarer is not brought closer by Butcher's Hook (it is not a Core ability).
    /// Dev: the hook as the first ability, rank 1: the second application is at 60 seconds.
    /// </summary>
    [Fact]
    public void The_Wayfarer_Is_Not_Brought_Closer_By_The_Hook()
    {
        var f = Fight("Gunde", Wayfarer, 1);
        f.Cast<ButchersHook>();
        f.Wait(65 - f.Now.TotalSeconds);
        var gains = f.Events.Where(e => e.Kind == StepEventKind.AuraGained && e.Name == "The Wayfarer").Select(e => e.Time.TotalSeconds).ToList();
        Assert.Equal(2, gains.Count);
        Assert.Equal(60.0, gains[1], 0.05);
    }

    /// <summary>
    /// Player: The Subduer does reach the hook: it deals 5% / 10% / 15% / 20% more damage to an enemy at 30% health or less.
    /// Dev: the hook's damage at exactly 30%, with and without the glyph; and at 30.000001% it is the same.
    /// </summary>
    [Fact]
    public void The_Subduer_Reaches_The_Hook()
    {
        double Damage(int glyph, int rank, double health)
        {
            var f = Fight("Gunde", glyph, rank);
            f.SetEnemyHealth(health);
            return Hit("Gunde", f, Category.Control)!.Value.Value;
        }
        Assert.Equal(1.2, Damage(Subduer, 4, 30_000_000) / Damage(0, 1, 30_000_000), 1e-9);
        Assert.Equal(1.0, Damage(Subduer, 4, 30_000_001) / Damage(0, 1, 30_000_001), 1e-9);
    }
}
