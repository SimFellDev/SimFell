using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Emerald Judgement with the Emerald gem's First Strike: the spike "also triggers the Emerald gem's First Strike" (combat logs: First Strike refreshes
/// with the spike's hit). First Strike (Emerald gem node 1, from 80 gem power): +5% Expertise for 15 seconds when you first damage an enemy.
/// Dev: over the six heroes. 100 Emerald gem power (only that node is active); 1000 Main Stat; First Strike is read from the hero's aura of that name.
/// </summary>
public class AspectComplexEmeraldFirstStrike
{
    private const string ProcKey = "emerald_judgement";

    public static IEnumerable<object[]> Heroes => GearHero.All;

    private static StepDriver Fight(GearHero hero, bool aspect = true) =>
        hero.Fight(AspectFights.With(aspect ? AspectFights.EmeraldJudgement : 0, 1, gems: [(GemColor.Emerald, 100)]));

    /// <summary>
    /// Player: the first damage to an enemy gives First Strike's Expertise, so the spike that comes from that very hit is already 5% bigger: 8000 x 1.05.
    /// Dev: the proc forced; the first spike against 8000 x 1.05 (x the class aura); First Strike is on the hero.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Spike_Is_Boosted_By_First_Strike(GearHero hero)
    {
        var f = Fight(hero);
        f.ForceProcs(key => key == ProcKey ? true : null);
        hero.CastProbe(f, hero.Unit);
        Assert.Equal(8000.0 * 1.05 * AspectFights.ClassDamageAura(hero), f.AspectEffectHits(93)[0].Value, 1e-6);
        Assert.True(f.Hero.Auras.GetStacks(f.Hero.Gems.OfType<Emerald>().First().FirstStrike!, f.Now) > 0);
    }

    /// <summary>
    /// Player: a spike landing later refreshes First Strike to its full 15 seconds again.
    /// Dev: a first hit with no proc (First Strike starts, 15s); ten seconds later a hit with the proc forced: First Strike ends 15 seconds after the
    /// last spike (a burn or bleed that ticks on makes several while the proc is forced), not 15 after the first hit.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Spike_Refreshes_First_Strike(GearHero hero)
    {
        var f = Fight(hero);
        bool on = false;
        f.ForceProcs(key => key == ProcKey ? on : null);
        hero.CastProbe(f, hero.Unit);
        double started = f.Events.First(e => e.Kind == StepEventKind.AuraGained && e.Name == "First Strike").Time.TotalSeconds;
        f.Wait(10);
        on = true;
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        on = false; // a burn or bleed would roll on and on; the last spike is the one that counts
        double spike = f.AspectEffectHits(93).Last().Time.TotalSeconds;
        f.Wait(20);
        double ended = f.Events.Last(e => e.Kind == StepEventKind.AuraRemoved && e.Name == "First Strike").Time.TotalSeconds;
        Assert.True(spike > started + 10.0 - 1e-9);
        Assert.Equal(spike + 15.0, ended, 0.05);
    }

    /// <summary>
    /// Player: without the aspect the spike does not exist and First Strike times as usual: 15 seconds from the first hit.
    /// Dev: the control of the test above: no aspect; a first hit, the second ten seconds later: First Strike ends 15 seconds after the first hit.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_The_Aspect_First_Strike_Is_Not_Refreshed(GearHero hero)
    {
        var f = Fight(hero, aspect: false);
        hero.CastProbe(f, hero.Unit);
        double started = f.Events.First(e => e.Kind == StepEventKind.AuraGained && e.Name == "First Strike").Time.TotalSeconds;
        f.Wait(10);
        AspectFights.CastWhenReady(f, hero, hero.Unit);
        f.Wait(20);
        double ended = f.Events.Last(e => e.Kind == StepEventKind.AuraRemoved && e.Name == "First Strike").Time.TotalSeconds;
        Assert.Equal(started + 15.0, ended, 0.05);
    }
}
