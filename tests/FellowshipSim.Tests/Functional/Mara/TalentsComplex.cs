using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using MaraTalents = FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's talents together, or with several abilities: Deadly Scheme's Energy stacks, Gushing Blood with Seething Poison, and Creeping Death.
/// Dev: damage is the expected value, so ratios are exact.
/// </summary>
public class MaraTalentsComplex
{
    private const double Ratio = 0.001;

    /// <summary>
    /// Player: Deadly Scheme: every 5 Energy you spend gives a stack; at 40 stacks (200 Energy) your next Queen's Fang or Arachnid Assault has +100%
    /// crit chance, for 12 seconds.
    /// Dev: 10 Backstabs (20 Energy each, 200 in all); then a Queen's Fang at 3 Combo Points has crit chance 1; the next one does not.
    /// </summary>
    [Fact]
    public void Deadly_Scheme_Gives_The_Next_Finisher_100_Percent_Crit_After_200_Energy_Spent()
    {
        var f = StepFights.Mara(talents: [MaraTalents.DeadlyScheme.TalentId]);
        for (int i = 0; i < 10; i++) f.Cast<Backstab>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();
        f.SetComboPoints(3);
        f.Cast<QueensFang>();

        var hits = f.Damage("Queen's Fang");
        Assert.Equal(1.0, hits[0].CritChance, 1e-9);
        Assert.Equal(0.0, hits[1].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Deadly Scheme does not come early: with less than 200 Energy spent the finisher has no extra crit chance.
    /// Dev: 9 Backstabs (180 Energy); a Queen's Fang has crit chance 0.
    /// </summary>
    [Fact]
    public void Deadly_Scheme_Needs_40_Stacks()
    {
        var f = StepFights.Mara(talents: [MaraTalents.DeadlyScheme.TalentId]);
        for (int i = 0; i < 9; i++) f.Cast<Backstab>();
        f.SetComboPoints(3);

        f.Cast<QueensFang>();

        Assert.Equal(0.0, f.Damage("Queen's Fang")[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Gushing Blood makes the bleed deal 50% more damage to an enemy with your Seething Poison, and Seething Poison itself adds 20% to
    /// the bleed: 1.8 times in all.
    /// Dev: a bleed with Seething Poison on the enemy over a bleed with none; the first ticks are 1.2 x 1.5 = 1.8 times apart.
    /// TODO: whether the two bonuses multiply (1.8) or add (1.7) is not in the tooltips; the engine multiplies. Check in game.
    /// </summary>
    [Fact(Skip = "Gushing Blood with Seething Poison: 1.8 (multiplied) or 1.7 (added): to check in game")]
    public void Gushing_Blood_And_Seething_Poison_Stack_On_The_Bleed()
    {
        var plain = StepFights.Mara();
        plain.SetComboPoints(1);
        plain.Cast<HemorrhagingStrike>();
        plain.Wait(4);

        var f = StepFights.Mara(talents: [MaraTalents.GushingBlood.TalentId]);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();
        f.SetComboPoints(1);
        f.Cast<HemorrhagingStrike>();
        f.Wait(4);

        Assert.Equal(1.8, f.FirstHit("Hemorrhage") / plain.FirstHit("Hemorrhage"), Ratio);
    }

    /// <summary>
    /// Player: Creeping Death: your poisons and bleeds deal 0.5% more damage for every 1% haste.
    /// Dev: the first tick of the bleed (at 1 Combo Point) at 20% and 40% haste over the one at no haste: 1 + 0.5 x haste.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Creeping_Death_Raises_Poison_And_Bleed_Damage_By_Half_The_Haste(double haste)
    {
        double Tick(double h)
        {
            var f = StepFights.Mara(h);
            f.SetComboPoints(1);
            f.Cast<HemorrhagingStrike>();
            f.Wait(4);
            return f.FirstHit("Hemorrhage");
        }

        Assert.Equal(1 + 0.5 * haste, Tick(haste) / Tick(0), Ratio);
    }
}
