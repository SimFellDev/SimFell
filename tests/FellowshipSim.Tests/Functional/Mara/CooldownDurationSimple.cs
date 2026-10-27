using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using MaraTalents = FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's base cooldowns and charges, and how haste affects them (Widow's Bite, Brooding Shadows and the auto attack are shortened by it).
/// Dev: a spent charge is timed by advancing the fight 10ms at a time until all charges are back, from the end of the cast; within 20ms.
/// </summary>
public class MaraCooldownDurationSimple
{
    private const double Tolerance = 0.02;

    private static double WaitUntilFull(StepDriver f, Ability ability)
    {
        var cooldown = f.Hero.CooldownOf(ability);
        while (cooldown.ChargesAt(f.Now) < cooldown.MaxCharges && f.Now.TotalSeconds < 400) f.Wait(0.01);
        return f.Now.TotalSeconds;
    }

    private static double CastStart(StepDriver f, string ability) =>
        f.Events.Last(e => e.Kind == StepEventKind.CastStarted && e.Name == ability).Time.TotalSeconds;

    /// <summary>
    /// Player: Widow's Bite has 3 charges of 9 seconds, and your haste speeds the recharge: 9 / (1 + haste) seconds.
    /// Dev: at 0% and 20% haste; one cast; the charge is back 9 / (1 + haste) seconds after it.
    /// TODO: fails with haste: the charge comes back about 0.17s late, as Rime's Cold Snap and Ardeos's Fire Ball (the faster rate starts after
    /// the first global cooldown).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Widows_Bite_Has_3_Charges_Recharging_With_Haste(double haste)
    {
        var f = StepFights.Mara(haste);
        var ability = f.Ability<WidowsBite>();
        Assert.Equal(3, f.Hero.CooldownOf(ability).MaxCharges);

        f.Cast<WidowsBite>();
        double start = CastStart(f, "Widow's Bite");

        Assert.Equal(start + 9 / (1 + haste), WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: Brooding Shadows has 2 charges of 15 seconds, and your haste speeds the recharge: 15 / (1 + haste) seconds.
    /// Dev: at 0% and 20% haste; one cast; the charge is back 15 / (1 + haste) seconds after it.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Brooding_Shadows_Has_2_Charges_Recharging_With_Haste(double haste)
    {
        var f = StepFights.Mara(haste);
        var ability = f.Ability<BroodingShadows>();
        Assert.Equal(2, f.Hero.CooldownOf(ability).MaxCharges);

        f.Cast<BroodingShadows>();
        double start = CastStart(f, "Brooding Shadows");

        Assert.Equal(start + 15 / (1 + haste), WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: the cooldowns of Maiden of Death (60 seconds), Final Stratagem (180) and Hemorrhaging Strike (2) are not shortened by haste.
    /// Dev: at 0% and 50% haste; one cast; the ability is ready again that long after it (Hemorrhaging Strike with 1 Combo Point).
    /// </summary>
    [Theory]
    [InlineData("Maiden of Death", 60, 0.0)]
    [InlineData("Maiden of Death", 60, 0.5)]
    [InlineData("Final Stratagem", 180, 0.0)]
    [InlineData("Final Stratagem", 180, 0.5)]
    [InlineData("Hemorrhaging Strike", 2, 0.0)]
    [InlineData("Hemorrhaging Strike", 2, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string name, double seconds, double haste)
    {
        var f = StepFights.Mara(haste);
        f.SetComboPoints(1);
        Ability ability = name switch
        {
            "Maiden of Death" => f.Ability<MaidenOfDeath>(),
            "Final Stratagem" => f.Ability<FinalStratagem>(),
            _ => f.Ability<HemorrhagingStrike>(),
        };

        f.Cast(ability);
        double start = CastStart(f, name);

        Assert.Equal(start + seconds, WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: with the Macabre Stratagem talent, Macabre Stratagem has a 180 second cooldown.
    /// Dev: the talent taken; one cast; ready again 180 seconds after it.
    /// </summary>
    [Fact]
    public void Macabre_Stratagem_Has_A_180_Second_Cooldown()
    {
        var f = StepFights.Mara(talents: [MaraTalents.MacabreStratagemTalent.TalentId]);
        var ability = f.Ability<MacabreStratagem>();

        f.Cast<MacabreStratagem>();
        double start = CastStart(f, "Macabre Stratagem");

        Assert.Equal(start + 180, WaitUntilFull(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: your auto attack swings every 1.5 seconds, shortened by haste.
    /// Dev: at 0% and 20% haste; one swing; the next one is ready 1.5 / (1 + haste) seconds after it.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void The_Auto_Attack_Swings_Every_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Mara(haste);
        var ability = f.Ability<Attack>();

        f.Cast<Attack>();
        double start = CastStart(f, "Attack");

        Assert.Equal(start + 1.5 / (1 + haste), WaitUntilFull(f, ability), Tolerance);
    }
}
