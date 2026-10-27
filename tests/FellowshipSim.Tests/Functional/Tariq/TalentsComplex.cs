using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using TariqTalents = FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's talents together or with several abilities: Mouth for War with Focused Wrath, High Road with Mouth for War, the damage bonuses on one Skull
/// Crusher, Sledgehammer with those bonuses, Chain Lightning's talents together, everything a Heavy Strike gives.
/// Dev: talents are taken together through <c>StepFights.Tariq(talents: ...)</c>; chances are forced.
/// </summary>
public class TariqTalentsComplex
{
    private const double Ratio = 0.001;

    private static StepDriver Fight(int[] talents, int enemies = 1, double crit = 0)
    {
        var f = StepFights.Tariq(enemies: enemies, talents: talents, crit: crit);
        f.SetFury(100);
        return f;
    }

    private static double PlainCrusher()
    {
        var f = StepFights.Tariq();
        f.SetFury(100);
        f.Cast<SkullCrusher>();
        return f.FirstDamageOf("Skull Crusher");
    }

    /// <summary>
    /// Player: a Leap Smash with High Road and Mouth for War gives 20 Fury and a stack of Focused Wrath together, as it lands.
    /// Dev: both talents; no Fury; one Leap Smash: nothing at the cast, 20 Fury and 1 stack 0.5s later.
    /// </summary>
    [Fact]
    public void High_Road_And_Mouth_For_War_Both_Trigger_When_Leap_Smash_Lands()
    {
        var f = StepFights.Tariq(talents: [TariqTalents.HighRoad.TalentId, TariqTalents.MouthForWar.TalentId]);

        f.Cast<LeapSmash>();
        Assert.Equal(0, f.Fury());
        Assert.Equal(0, f.Stacks(FocusedWrath.Buff));
        f.Wait(0.5);

        Assert.Equal(20, f.Fury(), 1e-9);
        Assert.Equal(1, f.Stacks(FocusedWrath.Buff));
    }

    /// <summary>
    /// Player: Focused Wrath holds 3 stacks at most: the 2 of the ability and 1 from Mouth for War fill it, and another Leap Smash adds none.
    /// Dev: Mouth for War; Focused Wrath (2 stacks); a Leap Smash: 3 stacks; its cooldown reset and another Leap Smash: still 3.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Holds_3_Stacks_At_Most()
    {
        var f = StepFights.Tariq(talents: [TariqTalents.MouthForWar.TalentId]);
        f.Cast<FocusedWrath>();

        f.Cast<LeapSmash>();
        f.Wait(0.5);
        Assert.Equal(3, f.Stacks(FocusedWrath.Buff));

        f.Hero.CooldownOf(f.Ability<LeapSmash>()).ResetAll();
        f.Cast<LeapSmash>();
        f.Wait(0.5);
        Assert.Equal(3, f.Stacks(FocusedWrath.Buff));
    }

    /// <summary>
    /// Player: the stacks of Mouth for War make your next spenders cheaper like Focused Wrath's: a stack from a Leap Smash lets a Skull Crusher cost 12.
    /// Dev: Mouth for War; one Leap Smash; a Skull Crusher: 12 Fury spent.
    /// </summary>
    [Fact]
    public void A_Stack_From_Mouth_For_War_Makes_The_Next_Skull_Crusher_Cheaper()
    {
        var f = Fight([TariqTalents.MouthForWar.TalentId]);
        f.Cast<LeapSmash>();
        f.Wait(0.5);

        f.Cast<SkullCrusher>();

        Assert.Equal([12.0], f.FurySpent("Fury"));
    }

    /// <summary>
    /// Player: the damage bonuses on one Skull Crusher multiply: Focused Wrath (+10%) and a Schism (+150%) deal 1.1 x 2.5 = 2.75 times the damage.
    /// Dev: Schism's Skull Crusher stack set; Focused Wrath; one Skull Crusher; its hit over a plain one is 2.75; both stacks used up.
    /// </summary>
    [Fact]
    public void Focused_Wrath_And_Schism_Multiply_On_One_Skull_Crusher()
    {
        double plain = PlainCrusher();
        var f = Fight([TariqTalents.Schism.TalentId]);
        f.ForceProcs(key => key is TariqTalents.Schism.SkullCrusherProcKey or TariqTalents.Schism.HammerStormProcKey ? false : null);
        f.Hero.Auras.Apply(TariqTalents.Schism.SkullCrusherBuff, f.Now);
        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();

        Assert.Equal(1.1 * 2.5, f.FirstDamageOf("Skull Crusher") / plain, Ratio);
        Assert.Equal(0, f.Stacks(TariqTalents.Schism.SkullCrusherBuff));
        Assert.Equal(1, f.Stacks(FocusedWrath.Buff));
    }

    /// <summary>
    /// Player: Schism keeps its bonus over a whole Hammer Storm: all 3 spins deal 150% more.
    /// Dev: Schism's Hammer Storm stack set; one Hammer Storm; the 3 spins are 2.5 times those of a plain one, in the same 1 : 1.35 : 1.8225 ratio.
    /// </summary>
    [Fact]
    public void Schism_Boosts_All_Three_Spins_Of_Hammer_Storm()
    {
        var plain = StepFights.Tariq();
        plain.SetFury(100);
        plain.Cast<HammerStorm>();
        plain.Wait(1.5);
        var f = Fight([TariqTalents.Schism.TalentId]);
        f.ForceProcs(key => key is TariqTalents.Schism.SkullCrusherProcKey or TariqTalents.Schism.HammerStormProcKey ? false : null);
        f.Hero.Auras.Apply(TariqTalents.Schism.HammerStormBuff, f.Now);

        f.Cast<HammerStorm>();
        f.Wait(1.5);

        var spins = f.Damage("Hammer Storm");
        var plainSpins = plain.Damage("Hammer Storm");
        for (int i = 0; i < 3; i++) Assert.Equal(2.5, spins[i].Value / plainSpins[i].Value, Ratio);
        Assert.Equal(0, f.Stacks(TariqTalents.Schism.HammerStormBuff));
    }

    /// <summary>
    /// Player: Sledgehammer takes 10% of the Skull Crusher hit as it was dealt, with the bonuses on it: with Focused Wrath and Them Bones's crit the splash
    /// is still a tenth of the hit.
    /// Dev: 3 enemies; Focused Wrath; Them Bones forced on; one Skull Crusher; each splash is 0.1 of the physical hit.
    /// </summary>
    [Fact]
    public void Sledgehammer_Takes_A_Tenth_Of_The_Boosted_Hit()
    {
        var f = Fight([TariqTalents.Sledgehammer.TalentId, TariqTalents.ThemBones.TalentId], enemies: 3);
        f.ForceProcs(key => key == TariqTalents.ThemBones.ProcKey ? true : null);
        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();

        var hit = f.Damage("Skull Crusher").Single();
        var splashes = f.Damage("Sledgehammer");
        Assert.Equal(2, splashes.Count);
        Assert.All(splashes, s => Assert.Equal(0.1, s.Value / hit.Value, Ratio));
    }

    /// <summary>
    /// Player: Thunderstruck and The Motherload together: 9 hits, the bonus growing with the enemies the arc has hit.
    /// Dev: 3 enemies; Thunder Call; one Chain Lightning; 9 hits; the hits over the first are 1, 1.65, 2.3 and then 2.3.
    /// </summary>
    [Fact]
    public void Thunderstruck_And_The_Motherload_Add_Up_On_One_Arc()
    {
        var f = Fight([TariqTalents.Thunderstruck.TalentId, TariqTalents.TheMotherload.TalentId], enemies: 3);
        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(9, hits.Count);
        var expected = new[] { 1.0, 1.65, 2.3, 2.3, 2.3, 2.3, 2.3, 2.3, 2.3 };
        for (int i = 0; i < 9; i++) Assert.Equal(expected[i], hits[i].Value / hits[0].Value, Ratio);
    }

    /// <summary>
    /// Player: the free Chain Lightning of Ace of Spades is a full one: it has Thunderstruck's bounces too.
    /// Dev: Ace of Spades and Thunderstruck; the proc forced on after 10 seconds; one Skull Crusher (with no Thunder Call, so it deals one hit and rolls
    /// once); 9 Chain Lightning hits.
    /// TODO: this relies on the free Chain Lightning working without Thunder Call (see Blood &amp; Thunder's skipped test). Check in game.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Free_Chain_Lightning_Has_Thunderstruck_Bounces()
    {
        var f = Fight([TariqTalents.AceOfSpades.TalentId, TariqTalents.Thunderstruck.TalentId]);
        f.ForceProcs(key => key == TariqTalents.AceOfSpades.ProcKey ? true : null);
        f.Wait(10);

        f.Cast<SkullCrusher>();
        f.Wait(1);

        Assert.Equal(9, f.Damage("Chain Lightning").Count);
    }

    /// <summary>
    /// Player: one Heavy Strike gives everything its talents give together: a stack of Square Hammer, a stack of Far Beyond Driven, and a roll for Kill Em
    /// All and for Blood &amp; Thunder.
    /// Dev: Square Hammer, Far Beyond Driven, Kill Em All and Blood &amp; Thunder; the procs forced off; one Heavy Strike: 1 stack of each aura; the 2 procs rolled
    /// once each.
    /// </summary>
    [Fact]
    public void A_Heavy_Strike_Triggers_All_Its_Talents()
    {
        var f = Fight([TariqTalents.SquareHammer.TalentId, TariqTalents.FarBeyondDriven.TalentId, TariqTalents.KillEmAll.TalentId, TariqTalents.BloodAndThunder.TalentId]);
        var procs = f.ForceProcs(key => key is TariqTalents.KillEmAll.ProcKey or TariqTalents.BloodAndThunder.ProcKey ? false : null);

        f.Cast<HeavyStrike>();

        Assert.Equal(1, f.Stacks(TariqTalents.SquareHammer.Stacks));
        Assert.Equal(1, f.Stacks(TariqTalents.FarBeyondDriven.Buff));
        Assert.Equal(1, procs.Rolls.Count(r => r.Key == TariqTalents.KillEmAll.ProcKey));
        Assert.Equal(1, procs.Rolls.Count(r => r.Key == TariqTalents.BloodAndThunder.ProcKey));
    }

    /// <summary>
    /// Player: Bloodline and Ride the Lightning add up: Fury comes from both while Thunder Call lasts.
    /// Dev: both talents; Thunder Call; 10.05 seconds; the Fury is more than Bloodline's 10 alone, by more than 10.
    /// </summary>
    [Fact]
    public void Bloodline_And_Ride_The_Lightning_Both_Give_Fury_During_Thunder_Call()
    {
        var bloodline = StepFights.Tariq(talents: [TariqTalents.Bloodline.TalentId]);
        bloodline.Cast<ThunderCall>();
        bloodline.Wait(10.05);
        var both = StepFights.Tariq(talents: [TariqTalents.Bloodline.TalentId, TariqTalents.RideTheLightning.TalentId]);
        both.Cast<ThunderCall>();

        both.Wait(10.05);

        Assert.True(both.Fury() - bloodline.Fury() > 10, $"{both.Fury()} against {bloodline.Fury()}");
    }
}
