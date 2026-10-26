using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearGem;

/// <summary>
/// The Emerald gem on every hero: First Strike (+5% / +15% Expertise for 15s the first time you damage each enemy), Tactician's Accumen (+2% / +6%
/// Expertise), Blessing of the Commander (cooldowns 4% / 12% shorter); Vanguard's Resolve adds Expertise rating and Sentinel's Bastion shields.
/// Dev: no character sheet. First Strike is read as the hero's Expertise after the first hit against before it; durations from the aura events.
/// </summary>
public class GemEmerald
{
    private const GemColor Color = GemColor.Emerald;

    /// <summary>
    /// Player: the first time you damage an enemy you gain +5% Expertise (+15% at rank II) for 15 seconds.
    /// Dev: Expertise right before and right after the hero's first basic hit, at 80 and 600 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void First_Strike_Gives_Expertise_On_The_First_Hit(GearHero hero)
    {
        foreach (var (power, bonus) in new[] { (80.0, 0.05), (600.0, 0.15) })
        {
            var f = GemFights.Fight(hero, Color, power);
            double before = f.ExpertiseNow();
            hero.CastProbe(f, hero.Basic);
            Assert.Equal(bonus, f.ExpertiseNow() - before, 1e-9);
        }
    }

    /// <summary>
    /// Player: First Strike lasts 15 seconds.
    /// Dev: the aura's removal comes 15.0s after it was gained (tolerance 20ms: an aura ends on the server's next frame, 1/60s).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void First_Strike_Lasts_Fifteen_Seconds(GearHero hero)
    {
        var f = GemFights.Fight(hero, Color, 80);
        hero.CastProbe(f, hero.Basic);
        f.Wait(30);
        var gained = f.Auras(StepEventKind.AuraGained, "First Strike").First();
        var removed = f.Auras(StepEventKind.AuraRemoved, "First Strike").First();
        Assert.Equal(15.0, (removed.Time - gained.Time).TotalSeconds, 0.02);
    }

    /// <summary>
    /// Player: First Strike is given again by the first hit on a new enemy (refreshing it), not by later hits on an enemy already hit.
    /// Dev: a hit on the only enemy at 0s; at 10s a second enemy has appeared and a hit on it refreshes the aura, so just after the first aura would have ended (15.5s after it was gained) the Expertise is still up;
    /// without the new enemy the same hit on the first enemy does not, so then the Expertise is gone. (A hit that reaches every enemy, as
    /// Mara's and Tariq's Basic abilities or Rime's swallows do, hits the new enemy too.)
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void First_Strike_Is_Refreshed_By_A_New_Enemy_Only(GearHero hero)
    {
        double ExpertiseAfterFirstEnds(bool newEnemy)
        {
            var f = GemFights.Fight(hero, Color, 80);
            hero.CastProbe(f, hero.Basic);
            f.Wait(10 - f.Now.TotalSeconds);
            var target = newEnemy ? f.AddEnemy() : f.Enemy;
            GearFights.Fill(f);
            f.Cast(hero.Get(f, hero.Basic), target);
            var gained = f.Auras(StepEventKind.AuraGained, "First Strike").First().Time.TotalSeconds;
            f.Wait(gained + 15.5 - f.Now.TotalSeconds);
            return f.ExpertiseNow();
        }
        Assert.Equal(0.05, ExpertiseAfterFirstEnds(newEnemy: true), 1e-9);
        Assert.Equal(0.0, ExpertiseAfterFirstEnds(newEnemy: false), 1e-9);
    }

    /// <summary>
    /// Player: Tactician's Accumen gives +2% Expertise (+6% at rank II).
    /// Dev: Expertise at 299 / 300 gem power (+0.02) and 1249 / 1250 (rank I to rank II: +0.04).
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Tacticians_Accumen_Adds_Expertise(GearHero hero)
    {
        Assert.Equal(0.02, GemFights.Delta(hero, Color, 299, 300, GemFights.ExpertiseNow), 1e-9);
        Assert.Equal(0.04, GemFights.Delta(hero, Color, 1249, 1250, GemFights.ExpertiseNow), 1e-9);
    }

    /// <summary>
    /// Player: Blessing of the Commander shortens the cooldown of your abilities by 4% (12% at rank II).
    /// Dev: the Major ability's cooldown, from its cast to its ready time, at 449 / 450 gem power (x0.96) and 1499 / 1500 (x0.88 / 0.96).
    /// TODO: which abilities it cuts (the engine takes every ability's cooldown; the Major probe is read here) is to check in game.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Blessing_Of_The_Commander_Shortens_Cooldowns(GearHero hero)
    {
        double Cooldown(double power) => GemFights.Fight(hero, Color, power).MajorCooldown(hero);
        Assert.Equal(0.96, Cooldown(450) / Cooldown(449), 0.001);
        Assert.Equal(0.88 / 0.96, Cooldown(1500) / Cooldown(1499), 0.001);
    }

    /// <summary>
    /// Player: Sentinel's Bastion (a shield) does not change your damage.
    /// Dev: the unit hit is the same at 219 / 220 and 999 / 1000 gem power.
    /// </summary>
    [Theory]
    [MemberData(nameof(GemFights.Heroes), MemberType = typeof(GemFights))]
    public void Sentinels_Bastion_Does_Not_Change_Damage(GearHero hero)
    {
        Assert.Equal(GemFights.UnitHit(hero, Color, 219), GemFights.UnitHit(hero, Color, 220), 1e-9);
        Assert.Equal(GemFights.UnitHit(hero, Color, 999), GemFights.UnitHit(hero, Color, 1000), 1e-9);
    }

    /// <summary>
    /// Player: Vanguard's Resolve gives 8 Expertise rating (24 at rank II).
    /// Dev: not testable as written: the engine leaves the rating to the character sheet, so a hero built from gem power gets nothing.
    /// </summary>
    [Fact(Skip = "Vanguard's Resolve's rating is only in a character sheet: a build from gem power ignores it")]
    public void Vanguards_Resolve_Adds_Expertise_Rating()
    {
        // TODO: decide whether a build without a sheet should get the node's rating (needs the rating-to-percent conversion at the hero's level).
    }
}
