using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's legendary items, each worn on a bare Tariq: Executioner's Grin (his wraps), Thundering Vortex (his harness) and Slayer's Mosh
/// (his helm). Any legendary also gives Mote of Creation's +10% main stat, so the damage tests wear the legendary in both fights.
/// </summary>
public class TariqLegendaries
{
    // ---- Executioner's Grin

    /// <summary>
    /// With Executioner's Grin, every ability you cast, your weapon's included, has a 6% chance to give you Executioner's Grin: one roll per
    /// cast, however many enemies it hits.
    ///
    /// Source: game data (6%) and combat logs (NOTES.md: a Fated Strike grants it, fXyzwGcQkTrLD34R fight 4). On 3 enemies.
    /// </summary>
    [Theory]
    [InlineData("Wild Swing")]
    [InlineData("Fated Strike")]
    public void Executioners_Grin_Has_A_6_Percent_Chance_On_Every_Cast(string ability)
    {
        var f = StepFights.Tariq(enemies: 3, weaponAbilityId: FatedStrike.AbilityId, legendaries: [ExecutionersGrin.WrapsItemId]);
        var procs = f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? false : null); // only to read the chance

        f.Cast(ability);

        Assert.Equal([0.06], procs.ChancesOf(ExecutionersGrin.ProcKey));
    }

    /// <summary>When Executioner's Grin procs, you have Executioner's Grin for 8 seconds from the cast that gave it.</summary>
    [Fact]
    public void Executioners_Grin_Lasts_8_Seconds()
    {
        var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]);
        f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? true : null); // every cast procs

        f.Cast<WildSwing>();
        double end = f.CompletedAt("Wild Swing") + 8.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(ExecutionersGrin.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(ExecutionersGrin.Buff));
    }

    /// <summary>Executioner's Grin stacks up to 2 times: three procs in a row leave 2 stacks.</summary>
    [Fact]
    public void Executioners_Grin_Stacks_Up_To_2()
    {
        var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]);
        f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? true : null); // every cast procs

        f.Cast<WildSwing>();
        f.CastWhenReady("Wild Swing");
        f.CastWhenReady("Wild Swing");

        Assert.Equal(2, f.Stacks(ExecutionersGrin.Buff));
    }

    /// <summary>
    /// Executioner's Grin makes your next Culling Strike usable on any enemy regardless of its health: on an enemy at full health it is
    /// refused without the buff and allowed with it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Executioners_Grin_Makes_Culling_Strike_Usable_At_Full_Health(bool grin)
    {
        var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]);
        f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? false : null); // no new proc
        if (grin) f.Apply(ExecutionersGrin.Buff); // a proc earlier

        Assert.Equal(grin, f.TryCast<CullingStrike>().Succeeded);
    }

    /// <summary>The Culling Strike Executioner's Grin empowers costs no Fury: at 50 Fury it spends none, and 50 are left.</summary>
    [Fact]
    public void Executioners_Grin_Culling_Strike_Costs_No_Fury()
    {
        var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]);
        f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? false : null); // no new proc
        f.Set(Tariq.Fury, 50);
        f.SetHealth(0.2); // in execute range, where it would spend Fury
        f.Apply(ExecutionersGrin.Buff); // a proc earlier

        f.Cast<CullingStrike>();

        using var check = new Checks(f);
        check.Equal(0, f.Spends("Fury").Count);
        check.Equal(50, f.Amount(Tariq.Fury));
    }

    /// <summary>
    /// The Culling Strike Executioner's Grin empowers deals damage as if it spent 30 Fury, beyond the usual 10 Fury cap: 1 + 0.4 x 30 =
    /// 13 times a Culling Strike that spends no Fury.
    ///
    /// Source: the legendary's text and combat logs (NOTES.md, fXyzwGcQkTrLD34R fight 4; the effect's own text says 20).
    /// </summary>
    [Fact]
    public void Executioners_Grin_Culling_Strike_Deals_As_If_It_Spent_30_Fury()
    {
        double CullingStrikeHit(bool grin)
        {
            var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]).WithoutDamageSpread();
            f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? false : null); // no new proc
            f.SetHealth(0.2); // in execute range, for the plain one
            if (grin) f.Apply(ExecutionersGrin.Buff); // a proc earlier

            f.Cast<CullingStrike>(); // with no Fury

            return f.Damage<CullingStrike>().Single().Value;
        }

        AssertRatio(13.0, CullingStrikeHit(true) / CullingStrikeHit(false)); // 1 + 40% x 30 Fury
    }

    /// <summary>Each empowered Culling Strike uses up one stack of Executioner's Grin: of 2 stacks, 1 is left after it.</summary>
    [Fact]
    public void Executioners_Grin_Culling_Strike_Uses_One_Stack()
    {
        var f = StepFights.Tariq(legendaries: [ExecutionersGrin.WrapsItemId]);
        f.ForceProcs(key => key == ExecutionersGrin.ProcKey ? false : null); // no new proc
        f.Apply(ExecutionersGrin.Buff, stacks: 2); // two procs earlier

        f.Cast<CullingStrike>();

        Assert.Equal(1, f.Stacks(ExecutionersGrin.Buff));
    }

    // ---- Thundering Vortex

    /// <summary>
    /// With Thundering Vortex, Chain Lightning generates 50% more Fury: 1.8 per hit instead of 1.2, 6 gains of 1.8 on one enemy.
    ///
    /// From the engine (ThunderingVortex.cs, game data), marked UNVERIFIED: no log has this legendary. TODO: confirm the 1.5 multiplier in
    /// a log with Thundering Vortex.
    /// </summary>
    [Fact]
    public void Thundering_Vortex_Makes_Chain_Lightning_Give_1_8_Fury_Per_Hit()
    {
        var f = StepFights.Tariq(legendaries: [ThunderingVortex.HarnessItemId]);

        f.Cast<ThunderCall>(); // enables Chain Lightning
        f.Cast<ChainLightning>();

        using var check = new Checks(f);
        check.Equal(6, f.Gains("Fury").Count);
        foreach (var gain in f.Gains("Fury")) check.Equal(1.2 * 1.5, gain.Value, 1e-9, what: "a Chain Lightning hit's Fury");
    }

    /// <summary>
    /// With Thundering Vortex, each time Chain Lightning bounces you gain a stack of Thundering Vortex: its 6 hits are 5 bounces, 5 stacks.
    ///
    /// From the engine (ThunderingVortex.cs), marked UNVERIFIED: no log has this legendary. TODO: confirm in a log whether the first hit
    /// counts as a bounce.
    /// </summary>
    [Fact]
    public void Thundering_Vortex_Gives_A_Stack_Per_Chain_Lightning_Bounce()
    {
        var f = StepFights.Tariq(enemies: 3, legendaries: [ThunderingVortex.HarnessItemId]);

        f.Cast<ThunderCall>(); // enables Chain Lightning
        f.Cast<ChainLightning>();

        Assert.Equal(5, f.Stacks(ThunderingVortex.Buff));
    }

    /// <summary>
    /// With 20 stacks of Thundering Vortex, your next Chain Lightning deals double damage on every hit; with 19 it deals its usual damage.
    ///
    /// From the engine (ThunderingVortex.cs, game data), marked UNVERIFIED: no log has this legendary. TODO: confirm the 20 stacks and the
    /// x2 in a log.
    /// </summary>
    [Theory]
    [InlineData(19, 1.0)]
    [InlineData(20, 2.0)]
    public void Thundering_Vortex_At_20_Stacks_Doubles_The_Next_Chain_Lightning(int stacks, double times)
    {
        List<double> ChainLightningHits(int stacks)
        {
            var f = StepFights.Tariq(legendaries: [ThunderingVortex.HarnessItemId]).WithoutDamageSpread();
            if (stacks > 0) f.Apply(ThunderingVortex.Buff, stacks: stacks); // bounces earlier

            f.Cast<ThunderCall>(); // enables Chain Lightning
            f.Cast<ChainLightning>();

            return f.Damage<ChainLightning>().Select(hit => hit.Value).ToList();
        }

        var vortexHits = ChainLightningHits(stacks);
        var plainHits = ChainLightningHits(0);

        using var check = new Checks();
        check.Equal(6, vortexHits.Count);
        for (int i = 0; i < vortexHits.Count; i++) check.Ratio(times, vortexHits[i] / plainHits[i], what: $"hit {i + 1}");
    }

    /// <summary>
    /// The empowered Chain Lightning uses 20 stacks of Thundering Vortex, then its 5 bounces add 5: from 20 stacks 5 are left, from 30
    /// 15; from 19 nothing is used, and 24 are left.
    ///
    /// From the engine (ThunderingVortex.cs), marked UNVERIFIED: no log has this legendary. TODO: confirm in a log.
    /// </summary>
    [Theory]
    [InlineData(19, 24)]
    [InlineData(20, 5)]
    [InlineData(30, 15)]
    public void Thundering_Vortex_Empowered_Chain_Lightning_Uses_20_Stacks(int stacks, int left)
    {
        var f = StepFights.Tariq(legendaries: [ThunderingVortex.HarnessItemId]);
        f.Apply(ThunderingVortex.Buff, stacks: stacks); // bounces earlier

        f.Cast<ThunderCall>(); // enables Chain Lightning
        f.Cast<ChainLightning>();

        Assert.Equal(left, f.Stacks(ThunderingVortex.Buff));
    }

    /// <summary>
    /// Thundering Vortex stacks up to 40 times. With Ace of Spades, a Hammer Storm whose first spin procs on each of 10 enemies casts 10
    /// free Chain Lightnings at once; none finds 20 stacks to use, and their 50 bounces leave 40 stacks.
    ///
    /// From the engine (ThunderingVortex.cs, game data), marked UNVERIFIED: no log has this legendary. A Chain Lightning's bounces are fixed
    /// (5, or 8 with Thunderstruck), whatever the enemies, and a cast with 20 stacks uses them first, so one chain at a time peaks at
    /// 19 + 8 = 27: only chains cast together pass 40. Only the first spin's 10 rolls proc, so no later chain uses stacks.
    /// TODO: confirm the 40 stacks in a log.
    /// </summary>
    [Fact]
    public void Thundering_Vortex_Stacks_Up_To_40()
    {
        var f = StepFights.Tariq(enemies: 10, talents: [AceOfSpades.TalentId], legendaries: [ThunderingVortex.HarnessItemId]);
        f.Set(Tariq.Fury, 100); // enough for Hammer Storm
        int rolls = 0;
        f.ForceProcs(key => key == AceOfSpades.ProcKey ? rolls++ < 10 : null); // the first spin's hit on each enemy procs, no later one

        f.Cast<HammerStorm>(); // no Thunder Call: one hit per enemy and spin
        f.Wait(1.0); // every chain has landed

        using var check = new Checks(f);
        check.Equal(10 * 6, f.Damage<ChainLightning>().Count);
        check.Equal(40, f.Stacks(ThunderingVortex.Buff));
    }

    // ---- Slayer's Mosh

    /// <summary>With Slayer's Mosh, every enemy your Leap Smash hits gets Slayer's Mosh: all 3 of 3 enemies, as the leap lands.</summary>
    [Fact]
    public void Slayers_Mosh_Marks_Every_Enemy_Leap_Smash_Hits()
    {
        var f = StepFights.Tariq(enemies: 3, legendaries: [SlayersMosh.HelmItemId]);

        f.Cast<LeapSmash>(crit: false);
        f.Wait(0.5); // the leap lands

        using var check = new Checks(f);
        check.Equal(3, f.Damage<LeapSmash>().Count);
        foreach (var enemy in f.Enemies()) check.True(f.Has(SlayersMosh.Debuff, on: enemy), what: $"{enemy.Name} has Slayer's Mosh");
    }

    /// <summary>
    /// Slayer's Mosh lasts 6 seconds from the Leap Smash hit that gave it.
    ///
    /// Source: game data effect 2250 (6 seconds).
    /// </summary>
    [Fact]
    public void Slayers_Mosh_Lasts_6_Seconds()
    {
        var f = StepFights.Tariq(legendaries: [SlayersMosh.HelmItemId]);

        f.Cast<LeapSmash>(crit: false);
        f.Wait(0.5); // the leap lands
        double end = f.Damage<LeapSmash>().Single().Time.TotalSeconds + 6.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(SlayersMosh.Debuff, on: f.Enemy));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(SlayersMosh.Debuff, on: f.Enemy));
    }

    /// <summary>
    /// An enemy with Slayer's Mosh takes 20% more damage from you: a Skull Crusher just before the debuff's end deals 1.2 times a
    /// Skull Crusher with no leap, and one just after it 1.0 times.
    ///
    /// Source: game data effect 2250 (1.2).
    /// </summary>
    [Theory]
    [InlineData(-0.1, 1.2)]
    [InlineData(0.1, 1.0)]
    public void Slayers_Mosh_Makes_The_Enemy_Take_20_Percent_More_Damage(double fromEnd, double times)
    {
        double SkullCrusherHit(bool leap)
        {
            var f = StepFights.Tariq(legendaries: [SlayersMosh.HelmItemId]).WithoutDamageSpread();
            f.Set(Tariq.Fury, 100); // enough for Skull Crusher

            if (leap)
            {
                f.Cast<LeapSmash>(crit: false);
                f.Wait(0.5); // the leap lands
                f.WaitUntil(f.Damage<LeapSmash>().Single().Time.TotalSeconds + 6.0 + fromEnd);
            }
            f.Cast<SkullCrusher>();

            return f.Damage<SkullCrusher>().Single().Value;
        }

        AssertRatio(times, SkullCrusherHit(true) / SkullCrusherHit(false));
    }
}
