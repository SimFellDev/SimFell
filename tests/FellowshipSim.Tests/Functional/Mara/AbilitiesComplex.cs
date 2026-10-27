using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using MaraTalents = FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's abilities together: Spirit Refund, Seething Poison and the bleed, Final Stratagem's reset, Matriarch Macabre and its shadowy clones, and
/// Macabre Stratagem.
/// Dev: damage is the expected value, so ratios are exact.
/// </summary>
public class MaraAbilitiesComplex
{
    private const double Ratio = 0.001;

    // ---- Spirit Refund

    /// <summary>
    /// Player: a Spirit Refund on a finisher gives back the Combo Points it spent and the Energy it cost, and a Spirit Point.
    /// Dev: Spirit Refund forced on; Energy at 100 (so the gain is not lost at the cap); 6 Combo Points and a Queen's Fang (40 Energy), then half a second; 6 Combo Points held again, an Energy gain
    /// of 40, and 1 Spirit Point.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_Combo_Points_Energy_And_A_Spirit_Point()
    {
        var f = StepFights.Mara(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetComboPoints(6);
        f.SetEnergy(100);

        f.Cast<QueensFang>();
        f.Wait(0.5);

        Assert.Equal(6, f.ComboPoints(), 0.01);
        Assert.Contains(f.Gained("Energy"), g => Math.Abs(g - 40) < 0.01);
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints), 0.01);
    }

    /// <summary>
    /// Player: Spirit Refund only comes from abilities that spend Combo Points: a generator does not roll it.
    /// Dev: Spirit Refund forced on; a Backstab and a Widow's Bite; no refund roll, no Spirit Point.
    /// </summary>
    [Fact]
    public void Generators_Do_Not_Roll_A_Spirit_Refund()
    {
        var f = StepFights.Mara(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<Backstab>();
        f.Cast<WidowsBite>();

        Assert.DoesNotContain(procs.Rolls, r => r.Key == Hero.SpiritRefundProc);
        Assert.Equal(0, f.Hero.ResourceAmount(Hero.SpiritPoints), 0.01);
    }

    // ---- Seething Poison and the bleed

    /// <summary>
    /// Player: an enemy with Seething Poison on it takes 20% more damage from Hemorrhaging Strike's bleed.
    /// Dev: a bleed on an enemy, then the same with a Seething Poison from Stealth on it; the bleed's first tick is 1.2 times as strong.
    /// </summary>
    [Fact]
    public void Seething_Poison_Raises_The_Bleeds_Damage_By_20_Percent()
    {
        var plain = StepFights.Mara();
        plain.SetComboPoints(1);
        plain.Cast<HemorrhagingStrike>();
        plain.Wait(4);

        var poisoned = StepFights.Mara();
        poisoned.Cast<BroodingShadows>();
        poisoned.Cast<WidowsBite>();
        poisoned.SetComboPoints(1);
        poisoned.Cast<HemorrhagingStrike>();
        poisoned.Wait(4);

        Assert.Equal(1.2, poisoned.FirstHit("Hemorrhage") / plain.FirstHit("Hemorrhage"), Ratio);
    }

    // ---- Final Stratagem

    /// <summary>
    /// Player: Final Stratagem refills your Energy and Combo Points, and resets the cooldowns of your abilities, except its own.
    /// Dev: Widow's Bites, Brooding Shadows and Maiden of Death used, Energy at 50; Final Stratagem; 200 Energy and 6 Combo Points, the three
    /// abilities have all their charges back, and Final Stratagem is on cooldown.
    /// TODO: the engine's reset covers every ability but Final Stratagem, the Spirit ability and the auto attack, from the tooltip alone (no combat log has it).
    /// </summary>
    [Fact]
    public void Final_Stratagem_Refills_Resources_And_Resets_Cooldowns()
    {
        var f = StepFights.Mara();
        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();
        f.Cast<BroodingShadows>();
        f.Cast<MaidenOfDeath>();
        f.SetEnergy(50);

        f.Cast<FinalStratagem>();

        Assert.Equal(200, f.MaraOf().EnergyAt(f.Now), 0.01);
        Assert.Equal(6, f.ComboPoints(), 0.01);
        Assert.Equal(3, f.Hero.CooldownOf(f.Ability<WidowsBite>()).ChargesAt(f.Now));
        Assert.Equal(2, f.Hero.CooldownOf(f.Ability<BroodingShadows>()).ChargesAt(f.Now));
        Assert.Equal(0, f.Hero.GetCooldownRemains<MaidenOfDeath>(f.Now), 0.01);
        Assert.True(f.Hero.GetCooldownRemains<FinalStratagem>(f.Now) > 100);
    }

    /// <summary>
    /// Player: Macabre Stratagem replaces Final Stratagem when you have the talent, and cannot be used without it.
    /// Dev: without the talent Macabre Stratagem is refused; with it Final Stratagem is.
    /// </summary>
    [Fact]
    public void Macabre_Stratagem_Replaces_Final_Stratagem()
    {
        Assert.False(StepFights.Mara().TryCast<MacabreStratagem>().Succeeded);

        var f = StepFights.Mara(talents: [MaraTalents.MacabreStratagemTalent.TalentId]);

        Assert.False(f.TryCast<FinalStratagem>().Succeeded);
        Assert.True(f.TryCast<MacabreStratagem>().Succeeded);
    }

    // ---- Matriarch Macabre

    /// <summary>
    /// Player: Matriarch Macabre, your Spirit ability, needs Spirit Points. It gives Spirit of Heroism (+30% haste) and makes you deal 20% more
    /// damage for 20 seconds.
    /// Dev: refused without Spirit Points; with them, the haste is 30% higher, a Skittering Blades hit is 1.2 times as strong, and after 20
    /// seconds both are gone.
    /// </summary>
    [Fact]
    public void Matriarch_Macabre_Gives_30_Percent_Haste_And_20_Percent_Damage_For_20_Seconds()
    {
        var f = StepFights.Mara();
        Assert.False(f.TryCast<MatriarchMacabre>().Succeeded);
        f.Cast<SkitteringBlades>();
        double before = f.Damage("Skittering Blades")[0].Value;
        f.GiveMaraSpirit();

        f.Cast<MatriarchMacabre>();
        f.Cast<SkitteringBlades>();

        Assert.Equal(0.3, f.Hero.GetHaste(f.Now), 0.001);
        Assert.Equal(1.2, f.Damage("Skittering Blades")[1].Value / before, Ratio);

        f.Wait(20);
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), 0.001);
        f.Cast<SkitteringBlades>();
        Assert.Equal(1.0, f.Damage("Skittering Blades")[2].Value / before, Ratio);
    }

    /// <summary>
    /// Player: during Matriarch Macabre your Queen's Fang summons two shadowy clones that each repeat it for 50% of its damage.
    /// Dev: Matriarch Macabre, 3 Combo Points, a Queen's Fang; 2 clone hits, each half of the cast's own hit.
    /// </summary>
    [Fact]
    public void Queens_Fang_Summons_Two_Clones_At_50_Percent()
    {
        var f = StepFights.Mara();
        f.GiveMaraSpirit();
        f.Cast<MatriarchMacabre>();
        f.SetComboPoints(3);

        f.Cast<QueensFang>();
        f.Wait(2);

        var own = f.Damage("Queen's Fang");
        var clones = f.Damage("Queen's Fang (shadowy clone)");
        Assert.Single(own);
        Assert.Equal(2, clones.Count);
        Assert.All(clones, c => Assert.Equal(0.5, c.Value / own[0].Value, Ratio));
    }

    /// <summary>
    /// Player: the clones of an Arachnid Assault each hit every enemy as a single-target strike, at 50% of the damage, with no falloff.
    /// Dev: 10 enemies, Matriarch Macabre, 3 Combo Points, an Arachnid Assault; 10 hits of the cast's own, each below the clean damage by the
    /// falloff sqrt(8/10); 20 clone hits, each half of a hit with no falloff.
    /// </summary>
    [Fact]
    public void Arachnid_Assaults_Clones_Hit_Every_Enemy_Without_Falloff()
    {
        var f = StepFights.Mara(enemies: 10);
        f.GiveMaraSpirit();
        f.Cast<MatriarchMacabre>();
        f.SetComboPoints(3);

        f.Cast<ArachnidAssault>();
        f.Wait(2);

        var own = f.Damage("Arachnid Assault");
        var clones = f.Damage("Arachnid Assault (shadowy clone)");
        Assert.Equal(10, own.Count);
        Assert.Equal(20, clones.Count);
        Assert.All(clones, c => Assert.Equal(0.5 / Math.Sqrt(8.0 / 10.0), c.Value / own[0].Value, Ratio));
    }

    /// <summary>
    /// Player: Macabre Stratagem gives 10 seconds of Matriarch Macabre (and no extra haste); used again while it is active it extends it by 10
    /// seconds.
    /// Dev: the talent taken; a Macabre Stratagem; the buff is up and the haste unchanged; it is still up after 9 seconds and gone after 11.
    /// TODO: the extension (a second use while active, +10 seconds) is not tested: the 180 second cooldown stops a second use. Final Stratagem's reset
    /// does not apply to it either. Needs a cooldown reset to test.
    /// </summary>
    [Fact]
    public void Macabre_Stratagem_Gives_10_Seconds_Of_Matriarch_Macabre()
    {
        var f = StepFights.Mara(talents: [MaraTalents.MacabreStratagemTalent.TalentId]);

        f.Cast<MacabreStratagem>();

        Assert.True(f.Hero.Auras.HasAura(MatriarchMacabre.Buff, f.Now));
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), 0.001);
        f.Wait(9);
        Assert.True(f.Hero.Auras.HasAura(MatriarchMacabre.Buff, f.Now));
        f.Wait(2);
        Assert.False(f.Hero.Auras.HasAura(MatriarchMacabre.Buff, f.Now));
    }
}
