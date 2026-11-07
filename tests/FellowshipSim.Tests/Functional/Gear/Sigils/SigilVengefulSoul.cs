using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Vengeful Soul (sigil 5): "Your critical strikes have a chance to increase your Main Stat by 3.0% / 5.1% / 7.2% / 9.3% for 8 seconds. If
/// you are below 50% health when Vengeful Soul triggers, you are also healed [...]" (ranks 1 to 4). The heal is not tested: the sim takes
/// no damage.
/// </summary>
public class SigilVengefulSoul
{
    /// <summary>
    /// With Vengeful Soul, a critical strike can give you 3.0% / 5.1% / 7.2% / 9.3% more main stat for 8 seconds.
    ///
    /// Tariq casts Heavy Strike, since Wild Swing never crits. The 8 seconds run from the last hit, since each crit can give it again.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.03)]
    [InlineData("Rime", "Frost Bolt", 2, 0.051)]
    [InlineData("Rime", "Frost Bolt", 3, 0.072)]
    [InlineData("Rime", "Frost Bolt", 4, 0.093)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.093)]
    [InlineData("Elarion", "Focused Shot", 4, 0.093)]
    [InlineData("Gunde", "Double Strike", 4, 0.093)]
    [InlineData("Mara", "Skittering Blades", 4, 0.093)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.093)]
    public void Vengeful_Soul_Critical_Strikes_Give_Main_Stat_For_8_Seconds(string hero, string ability, int rank, double bonus)
    {
        var f = StepFights.Of(hero, augmentations: [new(VengefulSoul.SigilId, rank)]);
        f.ForceProcs(key => key == VengefulSoul.ProcKey ? true : null);
        double plainMainStat = f.MainStat();
        f.FillResources(); // enough to cast it

        f.Cast(ability, crit: true);
        f.Wait(1); // the hits land
        double start = f.Damage(ability).Last().Time.TotalSeconds;

        using var check = new Checks(f);
        check.Ratio(1 + bonus, f.MainStat() / plainMainStat);
        f.WaitUntil(start + 8 - 0.1);
        check.True(f.Has(VengefulSoul.Buff));
        f.WaitUntil(start + 8 + 0.1);
        check.False(f.Has(VengefulSoul.Buff));
    }

    /// <summary>Vengeful Soul rolls on your critical strikes only.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Vengeful_Soul_Rolls_On_Critical_Strikes_Only(bool crit)
    {
        var f = StepFights.Of("Rime", augmentations: [new(VengefulSoul.SigilId, 1)]);
        var procs = f.ForceProcs(key => key == VengefulSoul.ProcKey ? false : null); // only to count the rolls

        f.Cast("Frost Bolt", crit: crit);
        f.Wait(1); // the bolt lands

        Assert.Equal(crit, procs.ChancesOf(VengefulSoul.ProcKey).Count > 0);
    }
}
