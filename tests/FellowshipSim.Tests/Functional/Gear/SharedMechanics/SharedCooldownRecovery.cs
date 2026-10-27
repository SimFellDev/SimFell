using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>
/// Cooldown recovery: effects that make cooldowns tick down faster (a rate of 2.0 means a cooldown of 40 seconds takes 20). Sources share one
/// pool: each adds its bonus part, so 1.1x and 1.5x make 1.6x, not 1.65x ("How Cooldowns Actually Work", measured from live logs).
/// Dev: an aura with a CooldownRecovery modifier is put on the hero; the hero's Major ability (every hero has one with a cooldown over 10 seconds,
/// at no haste) is cast, then cast again as soon as the engine accepts it (tried every 0.05 seconds): the wait after the cast is its cooldown over the
/// rate, within 0.1 seconds.
/// </summary>
public class SharedCooldownRecovery
{
    public static IEnumerable<object[]> Heroes => SharedFights.Heroes;

    /// <summary>How many seconds after its cast the hero's Major ability can be cast again, with the recoveries on the hero.</summary>
    private static (double Cooldown, double Wait) ReadyAfter(GearHero hero, params double[] recoveries)
    {
        var f = hero.Fight();
        int n = 0;
        foreach (double recovery in recoveries)
        {
            f.Hero.Auras.Apply(AuraDef.Internal($"Test recovery {n++}", 0, 1, StatModifier.Increased(StatType.CooldownRecovery, recovery)), f.Now);
        }
        var major = hero.Get(f, hero.Major);
        GearFights.Fill(f);
        f.Cast(major);
        double cast = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted && e.Name == major.Name).Time.TotalSeconds;
        for (int i = 0; i < 2000; i++)
        {
            double now = f.Now.TotalSeconds;
            GearFights.Fill(f);
            if (f.TryCast(major).Succeeded) return (major.Cooldown.TotalSeconds, now - cast);
            f.Wait(0.05);
        }
        throw new Exception($"{major.Name} never became ready");
    }

    private static void AssertReadyAt(GearHero hero, double rate, params double[] recoveries)
    {
        var (cooldown, wait) = ReadyAfter(hero, recoveries);
        Assert.True(cooldown > 10, "the Major ability should have a cooldown of over 10 seconds");
        Assert.Equal(cooldown / rate, wait, 0.1);
    }

    /// <summary>Player: with no recovery effect a cooldown ticks down at real time. Dev: rate 1.0.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Without_Recovery_Cooldowns_Run_In_Real_Time(GearHero hero) => AssertReadyAt(hero, 1.0);

    /// <summary>Player: a recovery effect of 2x makes a cooldown tick down twice as fast. Dev: rate 2.0.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Recovery_Effect_Speeds_Cooldowns_Up(GearHero hero) => AssertReadyAt(hero, 2.0, 2.0);

    /// <summary>Player: two recovery effects add their bonuses: 1.1x and 1.5x make 1.6x. Dev: rate 1.6.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Recovery_Effects_Add_Their_Bonus_Parts(GearHero hero) => AssertReadyAt(hero, 1.6, 1.1, 1.5);
}
