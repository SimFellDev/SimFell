using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>The cooldown rate: the Cooldown Recovery attribute, Haste on a charge effect, and time rates on one cooldown.</summary>
public class CooldownRateTests
{
    private static readonly AuraDef ThirtyHaste = AuraDef.Internal("test haste", 0, 1, StatModifier.Add(StatType.Haste, 0.3));

    private static Hero Fight(string name)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = name, PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        return hero;
    }

    [Fact]
    public void Multiplier_Sources_Add_And_Event_Horizon_Multiplies_Their_Sum()
    {
        var hero = Fight("Elarion");
        hero.Auras.Apply(ThirtyHaste, SimTime.Zero);
        double haste = 1.0 + hero.GetHaste(SimTime.Zero);
        hero.Auras.Apply(Chronoshift.Barrier, SimTime.Zero);
        hero.Auras.Apply(EventHorizon.Buff, SimTime.Zero);
        hero.Auras.Apply(FatedStrike.GloriousPurpose, SimTime.Zero);

        // The base rate and the aura rates add, and Haste multiplies the sum.
        Assert.Equal(10.0 * haste, hero.CooldownRate(hero.GetAbility<SkystridersGrace>()!, SimTime.Zero), 9);
    }

    [Fact]
    public void Haste_On_A_Charge_Effect_Adds_Outside_The_Compound()
    {
        var hero = Fight("Elarion");
        hero.Auras.Apply(ThirtyHaste, SimTime.Zero);
        double haste = hero.GetHaste(SimTime.Zero);
        hero.Auras.Apply(EventHorizon.Buff, SimTime.Zero);

        Assert.Equal(1.0 + haste + haste, hero.CooldownRate(hero.GetAbility<HighwindArrow>()!, SimTime.Zero), 9);
    }

    [Fact]
    public void A_Static_Cooldown_And_A_Weapon_Ability_Run_Outside_Cooldown_Recovery()
    {
        var hero = Fight("Mara");
        hero.Auras.Apply(Chronoshift.Barrier, SimTime.Zero);

        Assert.Equal(1.0, hero.CooldownRate(hero.GetAbility<Kick>()!, SimTime.Zero));
        Assert.Equal(1.0, hero.CooldownRate(new Chronoshift(hero), SimTime.Zero));
    }

    [Fact]
    public void Skylit_Grace_Counts_Each_Volley_Falling()
    {
        var hero = Fight("Elarion");
        var ctx = hero.SimContext!;
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        new SkylitGrace().Bind(hero);
        var grace = hero.GetAbility<SkystridersGrace>()!;
        var starfall = hero.GetAbility<StarfallVolley>()!;

        starfall.Execute(ctx, enemy);
        Assert.Equal(1.0 + SkylitGrace.Acceleration, hero.CooldownRate(grace, ctx.CurrentTime), 9);

        ctx.RunUntil(SimTime.FromSeconds(4));
        starfall.Execute(ctx, enemy);
        Assert.Equal(2, starfall.ActiveCount);
        Assert.Equal(1.0 + 2 * SkylitGrace.Acceleration, hero.CooldownRate(grace, ctx.CurrentTime), 9);

        ctx.RunUntil(SimTime.FromSeconds(9));
        Assert.Equal(1, starfall.ActiveCount);
        Assert.Equal(1.0 + SkylitGrace.Acceleration, hero.CooldownRate(grace, ctx.CurrentTime), 9);

        ctx.RunUntil(SimTime.FromSeconds(13));
        Assert.Equal(0, starfall.ActiveCount);
        Assert.Equal(1.0, hero.CooldownRate(grace, ctx.CurrentTime));
    }

    [Fact]
    public void A_Slavebind_Totem_Death_Gives_Breaker_Of_Chains_For_12_Seconds()
    {
        var hero = Fight("Mara");
        var ctx = hero.SimContext!;
        var other = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        var totem = ctx.AddEnemy(101, 1, BreakerOfChains.CreatureName, 1e12);
        var ward = hero.GetAbility<ShadowWard>()!;

        ctx.RemoveEnemy(other, defeated: true);
        Assert.Equal(1.0, hero.CooldownRate(ward, ctx.CurrentTime));

        ctx.RemoveEnemy(totem, defeated: true);
        Assert.Equal(1.3, hero.CooldownRate(ward, ctx.CurrentTime), 9);
        Assert.Equal(1.0, hero.CooldownRate(ward, ctx.CurrentTime + SimTime.FromSeconds(13)));
    }

    [Fact]
    public void Kill_Em_All_Adds_Its_Effect_Record_Value_Outside_Cooldown_Recovery()
    {
        var hero = Fight("Tariq");
        new KillEmAll().Bind(hero);
        hero.Auras.Apply(KillEmAll.Buff, SimTime.Zero);
        hero.Auras.Apply(FatedStrike.GloriousPurpose, SimTime.Zero);

        Assert.Equal(3.0 + 1.6, hero.CooldownRate(hero.GetAbility<HeavyStrike>()!, SimTime.Zero), 9);
    }
}
