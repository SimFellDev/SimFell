using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Dragonbone Dice, a relic's passive. Worn by a bare hero. The effect runs over every hero, since a relic works through what the hero
/// casts, not through named abilities; its details run on Rime. Every resource is filled before a cast, so any ability can be cast. Procs
/// are forced.
/// </summary>
public class RelicDragonboneDice
{
    /// <summary>
    /// With Dragonbone Dice, a critical strike can give you Draconic Might: 18% more main stat for 14 seconds.
    ///
    /// Tariq casts Heavy Strike, since Wild Swing never crits. The 14 seconds run from the last hit, since each crit can give it again.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Skittering Blades")]
    [InlineData("Tariq", "Heavy Strike")]
    public void Dragonbone_Dice_Critical_Strikes_Give_18_Percent_Main_Stat_For_14_Seconds(string hero, string ability)
    {
        var f = StepFights.Of(hero, relics: [DragonboneDice.OrbItemId]);
        f.ForceProcs(key => key == DragonboneDice.ProcKey ? true : null);
        double plainMainStat = f.MainStat();
        f.FillResources(); // enough to cast it

        f.Cast(ability, crit: true);
        f.Wait(1); // the hits land
        double start = f.Damage(ability).Last().Time.TotalSeconds;

        using var check = new Checks(f);
        check.Ratio(1.18, f.MainStat() / plainMainStat);
        f.WaitUntil(start + 14 - 0.1);
        check.True(f.Has(DragonboneDice.Buff));
        f.WaitUntil(start + 14 + 0.1);
        check.False(f.Has(DragonboneDice.Buff));
    }

    /// <summary>Dragonbone Dice rolls on your critical strikes only: a hit that does not crit never gives Draconic Might.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Dragonbone_Dice_Rolls_On_Critical_Strikes_Only(bool crit)
    {
        var f = StepFights.Of("Rime", relics: [DragonboneDice.OrbItemId]);
        var procs = f.ForceProcs(key => key == DragonboneDice.ProcKey ? false : null); // only to count the rolls

        f.Cast<FrostBolt>(crit: crit);
        f.Wait(1); // the hits land

        Assert.Equal(crit, procs.ChancesOf(DragonboneDice.ProcKey).Count > 0);
    }
}
