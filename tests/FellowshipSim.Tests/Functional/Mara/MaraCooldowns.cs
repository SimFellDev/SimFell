using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Mara's cooldowns last: the base durations and charges, and what haste does to them (Widow's Bite, Brooding Shadows and the
/// auto attack are shortened by it).
///
/// Every duration is checked on both sides of its end, 0.1s before and 0.1s after, measured from the cast (all of Mara's abilities are
/// instant).
/// </summary>
public class MaraCooldowns
{
    // ---- Base durations and haste

    /// <summary>The cooldowns of Maiden of Death (60 seconds) and Final Stratagem (180) are not shortened by haste.</summary>
    [Theory]
    [InlineData("Maiden of Death", 60, 0.0)]
    [InlineData("Maiden of Death", 60, 0.5)]
    [InlineData("Final Stratagem", 180, 0.0)]
    [InlineData("Final Stratagem", 180, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Mara(haste: haste);

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Hemorrhaging Strike

    /// <summary>
    /// Hemorrhaging Strike has no cooldown: only the 1 second global cooldown holds back a recast, so a second Hemorrhaging Strike goes
    /// out as soon as the first one's global cooldown is over, 1 second after it.
    ///
    /// Game data: its cooldown row has no value, so the cooldown is 0; combat logs: two casts 1.034s apart (NOTES.md, Hemorrhaging Strike).
    /// </summary>
    [Fact]
    public void Hemorrhaging_Strike_Has_No_Cooldown()
    {
        var f = StepFights.Mara();
        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        f.Cast<HemorrhagingStrike>();
        f.Set(Mara.ComboPoints, 1); // enough for the second

        Assert.True(f.IsReady<HemorrhagingStrike>());
        f.Cast<HemorrhagingStrike>();
        var casts = f.CastsCompleted("Hemorrhaging Strike");
        AssertSeconds(1.0, (casts[1].Time - casts[0].Time).TotalSeconds);
    }

    // ---- Widow's Bite

    /// <summary>
    /// Widow's Bite has 3 charges of 9 seconds, and haste speeds the recharge: a spent charge is back 9 / (1 + haste) seconds after the
    /// cast.
    ///
    /// Haste is an acceleration: the charge keeps its 9 seconds and recharges 1 + haste times as fast (recovery rate 1 + haste).
    ///
    /// TODO: fails with haste: the charge comes back about 0.17s late, as Rime's Cold Snap and Ardeos's Fire Ball (the faster rate starts
    /// after the first global cooldown).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Widows_Bite_Has_3_Charges_Recharging_With_Haste(double haste)
    {
        var f = StepFights.Mara(haste: haste);
        Assert.Equal(3, f.Charges<WidowsBite>());

        f.Cast<WidowsBite>();
        double end = f.CompletedAt("Widow's Bite") + 9 / (1 + haste);

        Assert.Equal(1 + haste, f.AccelerationRate<WidowsBite>());
        f.WaitUntil(end - 0.1);
        Assert.Equal(2, f.Charges<WidowsBite>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(3, f.Charges<WidowsBite>());
    }

    // ---- Brooding Shadows

    /// <summary>
    /// Brooding Shadows has 2 charges of 15 seconds, and haste speeds the recharge. The cast spends no charge; the attack that ends Stealth
    /// does, in combat: 2 charges after the cast, 1 after a Backstab from Stealth, and the charge back 15 / (1 + haste) seconds after it.
    /// Haste is an acceleration: the charge keeps its 15 seconds and recharges 1 + haste times as fast (recovery rate 1 + haste).
    ///
    /// Game data: a manual cooldown commitment, the Stealth monitor commits the cooldown as Stealth ends (NOTES.md, Stealth).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Brooding_Shadows_Spends_A_Charge_When_An_Attack_Ends_Stealth_Recharging_With_Haste(double haste)
    {
        var f = StepFights.Mara(haste: haste);

        f.Cast<BroodingShadows>();
        Assert.Equal(2, f.Charges<BroodingShadows>());

        f.Cast<StealthBackstab>(); // ends Stealth
        double end = f.CompletedAt("Stealth Backstab") + 15 / (1 + haste);

        Assert.Equal(1 + haste, f.AccelerationRate<BroodingShadows>());
        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<BroodingShadows>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<BroodingShadows>());
    }

    /// <summary>
    /// Casting Brooding Shadows again in Stealth ends Stealth and spends no charge: after the recast you are out of Stealth with 2 charges.
    /// </summary>
    [Fact]
    public void A_Recast_Of_Brooding_Shadows_Ends_Stealth_And_Spends_No_Charge()
    {
        var f = StepFights.Mara();

        f.Cast<BroodingShadows>();
        Assert.True(f.Has(BroodingShadows.Stealth));
        f.Wait(1); // past the padding

        f.Cast<BroodingShadows>();

        using var check = new Checks(f);
        check.False(f.Has(BroodingShadows.Stealth));
        check.Equal(2, f.Charges<BroodingShadows>());
    }

    // ---- Macabre Stratagem

    /// <summary>With the Macabre Stratagem talent, Macabre Stratagem has a 180 second cooldown.</summary>
    [Fact]
    public void Macabre_Stratagem_Has_A_180_Second_Cooldown()
    {
        var f = StepFights.Mara(talents: [MacabreStratagemTalent.TalentId]);

        f.Cast<MacabreStratagem>();
        double end = f.CompletedAt("Macabre Stratagem") + 180;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<MacabreStratagem>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<MacabreStratagem>());
    }

    // ---- Sinner's Pride

    /// <summary>
    /// With Sinner's Pride, each Combo Point you spend reduces the cooldown of Maiden of Death by 0.6 seconds: Maiden of Death, then a
    /// Queen's Fang with 6 Combo Points, and Maiden of Death is ready 60 - 6 x 0.6 = 56.4 seconds after the first cast.
    /// </summary>
    [Fact]
    public void Sinners_Pride_Reduces_Maiden_Of_Deaths_Cooldown_By_0_6_Seconds_Per_Combo_Point()
    {
        var f = StepFights.Mara(talents: [SinnersPride.TalentId]);

        f.Cast<MaidenOfDeath>();
        double end = f.CompletedAt("Maiden of Death") + 60 - 6 * 0.6;
        f.Set(Mara.ComboPoints, 6);
        f.Cast<QueensFang>();

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<MaidenOfDeath>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<MaidenOfDeath>());
    }

    // ---- Attack

    /// <summary>Your auto attack swings every 1.5 seconds, shortened by haste: 1.5 / (1 + haste).</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Auto_Attack_Swings_Every_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Mara(haste: haste);

        f.Cast<Attack>();
        double end = f.CompletedAt("Attack") + 1.5 / (1 + haste);

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<Attack>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<Attack>());
    }
}
