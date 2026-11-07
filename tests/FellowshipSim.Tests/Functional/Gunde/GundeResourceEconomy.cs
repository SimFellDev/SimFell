using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's resources, on a bare Gunde. What a Spirit Refund gives every hero is in Generic/SpiritRefund.cs.
/// </summary>
public class GundeResourceEconomy
{
    // ---- Spirit Refund

    /// <summary>
    /// Each cast of an ability that applies Rend rolls a single Spirit Refund, however many hits it lands and enemies it strikes.
    ///
    /// 3 enemies, so an area ability strikes several. TODO: confirm in the game that it is one roll per cast, as the user expects.
    /// </summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Grim Carve")]
    [InlineData("Heart Splitter")]
    [InlineData("Rupture")]
    [InlineData("Bloodbound Spirit")]
    public void Each_Cast_That_Applies_Rend_Rolls_One_Spirit_Refund(string ability)
    {
        var f = StepFights.Gunde(enemies: 3, spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to count the rolls
        f.FillResources(); // enough to cast it, Spirit Points included

        f.Cast(ability);
        f.Wait(5); // every hit lands

        Assert.Single(procs.ChancesOf(Hero.SpiritRefundProc));
    }
}
