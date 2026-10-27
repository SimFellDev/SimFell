using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using MaraTalents = FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's talents one at a time, as their tooltips describe them.
/// Dev: damage is the expected value, so ratios are exact; random procs are forced, and the chance the engine gives each is read from
/// <c>ForceProcs</c>'s <c>Rolls</c>. Talents no combat log has shown are marked unverified in the engine; the tests follow the tooltips.
/// </summary>
public class MaraTalentsSimple
{
    private const double Ratio = 0.001;

    private static StepDriver With(int talent, int enemies = 1, double haste = 0, double crit = 0) =>
        StepFights.Mara(haste, enemies, [talent], crit: crit);

    private static double Hit(StepDriver f, string ability) => f.Damage(ability).First().Value;

    /// <summary>A Queen's Fang hit at 3 Combo Points, with no talent: what a talent's bonus is compared with.</summary>
    private static double PlainQueensFang()
    {
        var f = StepFights.Mara();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();
        return Hit(f, "Queen's Fang");
    }

    // ---- Resources

    /// <summary>
    /// Player: Efficient Killer: each Combo Point you spend generates 1 Energy.
    /// Dev: Energy at 100; 5 Combo Points and a Queen's Fang; an Energy gain of 5.
    /// </summary>
    [Fact]
    public void Efficient_Killer_Generates_1_Energy_Per_Combo_Point_Spent()
    {
        var f = With(MaraTalents.EfficientKiller.TalentId);
        f.SetEnergy(100);
        f.SetComboPoints(5);

        f.Cast<QueensFang>();

        Assert.Contains(f.Gained("Energy"), g => Math.Abs(g - 5) < 0.01);
    }

    /// <summary>
    /// Player: Venomous Delight: each time you deal poison damage to an enemy you have a 10% chance to generate 10 Energy.
    /// Dev: a Widow's Bite from Stealth (Seething Poison); the proc is rolled with a 10% chance at each poison tick; forced on, the tick gives an
    /// Energy gain of 10 (Energy at 100 first).
    /// </summary>
    [Fact]
    public void Venomous_Delight_Has_A_10_Percent_Chance_Of_10_Energy_Per_Poison_Hit()
    {
        var f = With(MaraTalents.VenomousDelight.TalentId);
        var procs = f.ForceProcs(key => key == MaraTalents.VenomousDelight.ProcKey ? true : null);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();
        f.SetEnergy(100);

        f.Wait(2.5);

        Assert.NotEmpty(f.Damage("Seething Poison"));
        Assert.All(procs.Rolls.Where(r => r.Key == MaraTalents.VenomousDelight.ProcKey), r => Assert.Equal(0.1, r.Chance, 1e-9));
        Assert.Contains(f.Gained("Energy"), g => Math.Abs(g - 10) < 0.01);
    }

    /// <summary>
    /// Player: Sinner's Pride: each Combo Point you spend reduces the cooldown of Maiden of Death by 0.6 seconds.
    /// Dev: Maiden of Death used, then a Queen's Fang with 6 Combo Points a second later; Maiden of Death is ready 60 - 6 x 0.6 = 56.4 seconds
    /// after the first cast.
    /// </summary>
    [Fact]
    public void Sinners_Pride_Reduces_Maiden_Of_Deaths_Cooldown_By_0_6_Seconds_Per_Combo_Point()
    {
        var f = With(MaraTalents.SinnersPride.TalentId);
        var maiden = f.Ability<MaidenOfDeath>();
        f.Cast<MaidenOfDeath>();
        f.SetComboPoints(6);
        f.Cast<QueensFang>();

        var cooldown = f.Hero.CooldownOf(maiden);
        while (cooldown.ChargesAt(f.Now) < cooldown.MaxCharges && f.Now.TotalSeconds < 100) f.Wait(0.01);

        Assert.Equal(56.4, f.Now.TotalSeconds, 0.02);
    }

    // ---- Damage

    /// <summary>
    /// Player: Assassin's Guile: after attacking from Stealth, your Queen's Fang and Arachnid Assault deal 40% more damage for 5 seconds.
    /// Dev: a Skittering Blades from Stealth, then at once 3 Combo Points and a Queen's Fang: 1.4 times a plain one; after 5 seconds, 1.0 times.
    /// </summary>
    [Fact]
    public void Assassins_Guile_Raises_Finisher_Damage_By_40_Percent_For_5_Seconds()
    {
        double plain = PlainQueensFang();
        var f = With(MaraTalents.AssassinsGuile.TalentId);
        f.Cast<BroodingShadows>();
        f.Cast<StealthSkitteringBlades>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();
        f.Wait(6);
        f.SetComboPoints(3);
        f.Cast<QueensFang>();

        var hits = f.Damage("Queen's Fang");
        Assert.Equal(1.4, hits[0].Value / plain, Ratio);
        Assert.Equal(1.0, hits[1].Value / plain, Ratio);
    }

    /// <summary>
    /// Player: Feed the Queen: each enemy your Skittering Blades hits gives a stack, and your next Queen's Fang deals 12% more damage per stack
    /// (6 stacks at most) and uses them up.
    /// Dev: 3 enemies and a Skittering Blades (3 stacks), then a Queen's Fang at 3 Combo Points: 1.36 times a plain one; a second one is plain.
    /// </summary>
    [Fact]
    public void Feed_The_Queen_Raises_The_Next_Queens_Fang_By_12_Percent_Per_Stack()
    {
        double plain = PlainQueensFang();
        var f = With(MaraTalents.FeedTheQueen.TalentId, enemies: 3);
        f.Cast<SkitteringBlades>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();

        var hits = f.Damage("Queen's Fang");
        Assert.Equal(1.36, hits[0].Value / plain, Ratio);
        Assert.Equal(1.0, hits[1].Value / plain, Ratio);
    }

    /// <summary>
    /// Player: Malevolence: Queen's Fang gives a stack of Malevolence for Arachnid Assault, which makes your next Arachnid Assault deal 100% more
    /// damage; Arachnid Assault does the same for Queen's Fang.
    /// Dev: a Queen's Fang, then an Arachnid Assault at 3 Combo Points: 2 times a plain one; then a Queen's Fang (given by that Arachnid Assault)
    /// at 3 Combo Points: 2 times a plain one.
    /// </summary>
    [Fact]
    public void Malevolence_Doubles_The_Other_Finisher_After_Each_Finisher()
    {
        double plainFang = PlainQueensFang();
        var reference = StepFights.Mara();
        reference.SetComboPoints(3);
        reference.Cast<ArachnidAssault>();
        double plainAssault = Hit(reference, "Arachnid Assault");

        var f = With(MaraTalents.Malevolence.TalentId);
        f.SetComboPoints(3);
        f.Cast<QueensFang>();
        f.SetComboPoints(3);
        f.Cast<ArachnidAssault>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();

        Assert.Equal(1.0, f.Damage("Queen's Fang")[0].Value / plainFang, Ratio);
        Assert.Equal(2.0, f.Damage("Arachnid Assault")[0].Value / plainAssault, Ratio);
        Assert.Equal(2.0, f.Damage("Queen's Fang")[1].Value / plainFang, Ratio);
    }

    /// <summary>
    /// Player: Arachnid Onslaught: your Arachnid Assault deals 20% more damage to enemies that have your Hemorrhaging Strike bleed.
    /// Dev: a bleed on the enemy, then an Arachnid Assault at 3 Combo Points: 1.2 times a plain one.
    /// </summary>
    [Fact]
    public void Arachnid_Onslaught_Raises_Arachnid_Assault_By_20_Percent_Against_A_Bleeding_Enemy()
    {
        var reference = StepFights.Mara();
        reference.SetComboPoints(3);
        reference.Cast<ArachnidAssault>();
        double plain = Hit(reference, "Arachnid Assault");
        var f = With(MaraTalents.ArachnidOnslaught.TalentId);
        f.SetComboPoints(1);
        f.Cast<HemorrhagingStrike>();
        f.SetComboPoints(3);

        f.Cast<ArachnidAssault>();

        Assert.Equal(1.2, Hit(f, "Arachnid Assault") / plain, Ratio);
    }

    /// <summary>
    /// Player: Maiden's Doom: Maiden of Death's damage bonus is 40%, not 20%, against enemies with 30% health or less.
    /// Dev: Maiden of Death up; an enemy at 100% health and one at 20%: a Skittering Blades hit on each; the weak one's is 1.4 / 1.2 times the
    /// other's.
    /// </summary>
    [Fact]
    public void Maidens_Doom_Raises_Maiden_Of_Deaths_Bonus_To_40_Percent_On_Low_Enemies()
    {
        var f = With(MaraTalents.MaidensDoom.TalentId, enemies: 2);
        var weak = f.Context.AllEnemies[1];
        weak.CurrentHealth = weak.MaxHealth * 0.2;
        f.Cast<MaidenOfDeath>();

        f.Cast<SkitteringBlades>();

        var hits = f.Damage("Skittering Blades");
        double healthy = hits.First(h => h.Target == f.Enemy.Name).Value;
        double low = hits.First(h => h.Target == weak.Name).Value;
        Assert.Equal(1.4 / 1.2, low / healthy, Ratio);
    }

    // ---- Crit

    /// <summary>
    /// Player: Puncture: your Widow's Bite has +100% crit chance.
    /// Dev: a Widow's Bite with no crit chance of her own; its hits have crit chance 1.
    /// </summary>
    [Fact]
    public void Puncture_Gives_Widows_Bite_100_Percent_Crit_Chance()
    {
        var f = With(MaraTalents.Puncture.TalentId);

        f.Cast<WidowsBite>();

        Assert.All(f.Damage("Widow's Bite"), h => Assert.Equal(1.0, h.CritChance, 1e-9));
    }

    /// <summary>
    /// Player: Red Ledger: while your Hemorrhaging Strike bleed is on an enemy you have +5% crit chance, and +1% more for each further enemy it is
    /// on, up to +10% in all.
    /// Dev: bleeds put on 1, 3 and 7 enemies; the crit chance of a Skittering Blades hit is 5%, 7% and 10%.
    /// </summary>
    [Theory]
    [InlineData(1, 0.05)]
    [InlineData(3, 0.07)]
    [InlineData(7, 0.10)]
    public void Red_Ledger_Gives_5_Percent_Crit_Plus_1_Per_Further_Bleeding_Enemy_Up_To_10(int bleeding, double crit)
    {
        var f = With(MaraTalents.RedLedger.TalentId, enemies: bleeding);
        foreach (var enemy in f.Context.AllEnemies)
        {
            f.SetEnergy(200);
            f.SetComboPoints(1);
            f.Cast<HemorrhagingStrike>(enemy);
            f.Wait(1.1);
        }

        f.Cast<SkitteringBlades>();

        Assert.Equal(crit, f.Damage("Skittering Blades").Last().CritChance, 1e-9);
    }

    // ---- Bleed

    /// <summary>
    /// Player: Bloodrush: your Hemorrhaging Strike bleed ticks 20% faster.
    /// Dev: a bleed at no haste; its ticks come every 3 / 1.2 = 2.5 seconds.
    /// </summary>
    [Fact]
    public void Bloodrush_Makes_The_Bleed_Tick_20_Percent_Faster()
    {
        var f = With(MaraTalents.Bloodrush.TalentId);
        f.SetComboPoints(1);
        f.Cast<HemorrhagingStrike>();

        f.Wait(8);

        var ticks = f.Damage("Hemorrhage");
        Assert.True(ticks.Count >= 3);
        Assert.Equal(2.5, (ticks[1].Time - ticks[0].Time).TotalSeconds, 0.002);
    }

    /// <summary>
    /// Player: Gushing Blood: while Maiden of Death is active your Hemorrhaging Strike puts its bleed on up to 4 more enemies, and the bleed deals
    /// 50% more damage to enemies with your Seething Poison.
    /// Dev: 7 enemies and Maiden of Death; a Hemorrhaging Strike; 5 enemies bleed (the target and 4 more). The 50% against Seething Poison is
    /// in TalentsComplex.
    /// </summary>
    [Fact]
    public void Gushing_Blood_Spreads_The_Bleed_To_4_More_Enemies_During_Maiden_Of_Death()
    {
        var f = With(MaraTalents.GushingBlood.TalentId, enemies: 7);
        f.Cast<MaidenOfDeath>();
        f.SetComboPoints(1);

        f.Cast<HemorrhagingStrike>();

        Assert.Equal(5, f.MaraOf().Hemorrhage.ActiveCount(f.Context));
    }

    // ---- Procs

    /// <summary>
    /// Player: Caustic Wounds: your Backstab has a 30% chance to apply Caustic Poison outside Stealth if the enemy has your bleed.
    /// Dev: a bleed, then a Backstab; the roll has a 30% chance, forced on it gives a Caustic Poison hit; with no bleed there is no roll.
    /// </summary>
    [Fact]
    public void Caustic_Wounds_Has_A_30_Percent_Chance_On_A_Bleeding_Enemy()
    {
        var f = With(MaraTalents.CausticWounds.TalentId);
        var procs = f.ForceProcs(key => key == MaraTalents.CausticWounds.ProcKey ? true : null);
        f.Cast<Backstab>();
        Assert.DoesNotContain(procs.Rolls, r => r.Key == MaraTalents.CausticWounds.ProcKey);
        f.SetComboPoints(1);
        f.Cast<HemorrhagingStrike>();

        f.Cast<Backstab>();

        Assert.Equal(0.3, procs.Rolls.Single(r => r.Key == MaraTalents.CausticWounds.ProcKey).Chance, 1e-9);
        Assert.Single(f.Damage("Caustic Poison"));
    }

    /// <summary>
    /// Player: Seething Burst: each time Seething Poison deals damage it has a 20% chance to erupt for 150% of Agility on nearby enemies.
    /// Dev: a Widow's Bite from Stealth; at each tick the roll has a 20% chance; forced on, the eruption's hit over a Seething Poison tick is 1.5 / 1.2276.
    /// </summary>
    [Fact]
    public void Seething_Burst_Has_A_20_Percent_Chance_To_Erupt_For_150_Percent()
    {
        var f = With(MaraTalents.SeethingBurst.TalentId);
        var procs = f.ForceProcs(key => key == MaraTalents.SeethingBurst.ProcKey ? true : null);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();

        f.Wait(2.5);

        Assert.All(procs.Rolls.Where(r => r.Key == MaraTalents.SeethingBurst.ProcKey), r => Assert.Equal(0.2, r.Chance, 1e-9));
        double tick = f.Damage("Seething Poison").First().Value;
        double burst = f.Damage("Seething Burst").First().Value;
        Assert.Equal(1.5 / 1.2276, burst / tick, Ratio);
    }

    /// <summary>
    /// Player: Corrosive Spill: each Combo Point you spend has a 3% chance to coat the ground with a spill for 3 seconds, which deals 158% of Agility
    /// every 1.5 seconds.
    /// Dev: a finisher with 4 Combo Points rolls 4 times at 3%; forced on, 4 spills; each tick over a Skittering Blades hit is 1.58 / 0.806.
    /// </summary>
    [Fact]
    public void Corrosive_Spill_Has_A_3_Percent_Chance_Per_Combo_Point_Spent()
    {
        var reference = StepFights.Mara();
        reference.Cast<SkitteringBlades>();
        double unit = Hit(reference, "Skittering Blades") / 0.806;
        var f = With(MaraTalents.CorrosiveSpill.TalentId);
        var procs = f.ForceProcs(key => key == MaraTalents.CorrosiveSpill.ProcKey ? true : null);
        f.SetComboPoints(4);
        f.Cast<QueensFang>();

        f.Wait(5);

        var rolls = procs.Rolls.Where(r => r.Key == MaraTalents.CorrosiveSpill.ProcKey).ToList();
        Assert.Equal(4, rolls.Count);
        Assert.All(rolls, r => Assert.Equal(0.03, r.Chance, 1e-9));
        var ticks = f.EffectTicks(2310);
        Assert.NotEmpty(ticks);
        Assert.All(ticks, t => Assert.Equal(1.58, t.Value / unit, 1.58 * Ratio));
    }

    /// <summary>
    /// Player: Hemotoxin: your Backstab, Widow's Bite and auto attacks have a 12% chance to apply a stack of Hemotoxin, which deals 20% of Agility
    /// every 1.5 seconds over 9 seconds.
    /// Dev: a Backstab; the roll has a 12% chance; forced on, a stack; its tick over a Skittering Blades hit is 0.2 / 0.806, 1.5 seconds apart.
    /// </summary>
    [Fact]
    public void Hemotoxin_Has_A_12_Percent_Chance_To_Apply_A_Stack()
    {
        var reference = StepFights.Mara();
        reference.Cast<SkitteringBlades>();
        double unit = Hit(reference, "Skittering Blades") / 0.806;
        var f = With(MaraTalents.Hemotoxin.TalentId);
        var procs = f.ForceProcs(key => key == MaraTalents.Hemotoxin.ProcKey ? true : null);

        f.Cast<Backstab>();
        f.Wait(4);

        Assert.Equal(0.12, procs.Rolls.First(r => r.Key == MaraTalents.Hemotoxin.ProcKey).Chance, 1e-9);
        var ticks = f.EffectTicks(2337);
        Assert.True(ticks.Count >= 2);
        Assert.Equal(0.2, ticks[0].Value / unit, 0.2 * Ratio);
        Assert.Equal(1.5, (ticks[1].Time - ticks[0].Time).TotalSeconds, 0.002);
    }
}
