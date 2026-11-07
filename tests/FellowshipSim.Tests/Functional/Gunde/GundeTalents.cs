using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's talents.
/// </summary>
public class GundeTalents
{
    // ---- Murder of Crows

    /// <summary>With Murder of Crows, Rupture gives you 2 stacks of Murder of Crows.</summary>
    [Fact]
    public void Rupture_Gives_2_Stacks_Of_Murder_Of_Crows()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        Assert.Equal(0, f.Stacks(MurderOfCrows.Buff));

        f.Cast("Rupture");

        Assert.Equal(2, f.Stacks(MurderOfCrows.Buff));
    }

    /// <summary>
    /// Your next ability that applies Rend uses up a stack of Murder of Crows and always triggers a Spirit Refund: you get a Rising
    /// Spirit stack.
    ///
    /// The Spirit Refund's own roll is forced to fail, so the refund can only come from Murder of Crows.
    /// </summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Grim Carve")]
    [InlineData("Heart Splitter")]
    public void An_Ability_That_Applies_Rend_Uses_A_Stack_And_Always_Triggers_A_Spirit_Refund(string ability)
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Murder of Crows'
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");

        f.Cast(ability);
        f.Wait(1); // its first hit lands

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(MurderOfCrows.Buff));
        check.Equal(1, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>
    /// With a stack of Murder of Crows up, an ability that applies Rend does not roll for a Spirit Refund: it uses up the stack and gets
    /// the refund, even when the roll would have succeeded.
    ///
    /// The roll is forced to succeed, so an ability that rolled would keep the stack. Rupture's own roll is counted before.
    /// </summary>
    [Fact]
    public void An_Ability_Uses_A_Stack_Instead_Of_Rolling_For_A_Spirit_Refund()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a roll would succeed
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");
        f.Wait(1); // its first hit lands
        int rupturesRolls = procs.ChancesOf(Hero.SpiritRefundProc).Count;

        f.Cast("Double Strike");
        f.Wait(1); // its first hit lands

        using var check = new Checks(f);
        check.Equal(rupturesRolls, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls");
        check.Equal(1, f.Stacks(MurderOfCrows.Buff));
        check.Equal(2, f.Stacks(Hero.RisingSpirit)); // Rupture's, and Double Strike's
    }

    /// <summary>
    /// The 2 stacks give 2 certain Spirit Refunds: of three abilities that apply Rend after Rupture, the first two are refunded and the
    /// third is not.
    ///
    /// The Spirit Refund's own roll is forced to fail, as in the test above.
    /// </summary>
    [Fact]
    public void Two_Stacks_Give_Two_Certain_Spirit_Refunds()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Murder of Crows'
        f.FillResources(); // enough to cast them

        f.Cast("Rupture");

        f.Cast("Double Strike");
        f.Cast("Blood Arc");
        f.Cast("Heart Splitter");
        f.Wait(1); // their first hits land

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(MurderOfCrows.Buff));
        check.Equal(2, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>An ability that does not apply Rend leaves the Murder of Crows stacks alone.</summary>
    [Theory]
    [InlineData("Jawbreaker")]
    [InlineData("Reign in Blood")]
    public void An_Ability_That_Applies_No_Rend_Uses_No_Stack(string ability)
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");

        f.Cast(ability);
        f.Wait(1); // any hit lands

        Assert.Equal(2, f.Stacks(MurderOfCrows.Buff));
    }
}
