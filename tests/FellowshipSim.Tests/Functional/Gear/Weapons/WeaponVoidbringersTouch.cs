using FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Voidbringer's Touch (Void-touched Conduit), over every hero: "Apply Voidbringer's Touch to target enemy for 15 seconds, causing 10% of
/// all damage you deal to any enemy to be replicated and stored in the effect. Once Voidbringer's Touch expires, or a total of 4250% Main
/// Stat damage has been accumulated, the effect erupts in a shadowy explosion, instantly dealing the accumulated damage to the target.
/// Voidbringer's Touch has +100% critical strike chance. [...] always triggers if the damage it holds is greater than the remaining health
/// of the enemy [...] Can be cast during Global Cooldown."
///
/// Each hero stores damage with a single-target ability of their own; what it stores is every hit dealt before the eruption (Gunde's
/// Rend ticks included). Its cooldown, and casting it during the global cooldown, are in WeaponCooldowns.cs.
/// </summary>
public class WeaponVoidbringersTouch
{
    /// <summary>Voidbringer's Touch erupts when its 15 seconds run out, and deals nothing before.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Widow's Bite")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Voidbringers_Touch_Erupts_After_15_Seconds(string hero, string ability)
    {
        var f = StepFights.Of(hero, weaponAbilityId: VoidbringersTouch.AbilityId);
        f.FillResources(); // enough to cast it

        f.Cast<VoidbringersTouch>();
        double start = f.CompletedAt("Voidbringer's Touch");
        f.Cast(ability); // damage to store

        f.WaitUntil(start + 15 - 0.1);
        Assert.Empty(f.Damage<VoidbringersTouch>());
        f.WaitUntil(start + 15 + 0.1);
        Assert.Single(f.Damage<VoidbringersTouch>());
    }

    /// <summary>
    /// Voidbringer's Touch stores 10% of the damage you deal. Short of the cap, its eruption is the share of a hit at the cap that the
    /// stored damage is of the cap: a critical strike (x2), with its +100% crit chance, through your damage bonuses like any hit, your
    /// class's balancing aura included (Rime and Mara 1.04, Ardeos 1.1, Elarion 1.08, Gunde and Tariq 1.0).
    ///
    /// Short of the cap is rare in play: the touch usually erupts at the cap.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 1.1)]
    [InlineData("Elarion", "Focused Shot", 1.08)]
    [InlineData("Gunde", "Double Strike", 1.0)]
    [InlineData("Mara", "Widow's Bite", 1.04)]
    [InlineData("Tariq", "Skull Crusher", 1.0)]
    public void Voidbringers_Touch_Erupts_For_10_Percent_Of_Your_Damage_As_A_Critical_Strike(string hero, string ability, double balancing)
    {
        var f = StepFights.Of(hero, weaponAbilityId: VoidbringersTouch.AbilityId).WithoutDamageSpread();
        f.FillResources(); // enough to cast it

        f.Cast<VoidbringersTouch>();
        f.Cast(ability); // damage to store
        f.Wait(20); // the touch erupts

        var eruption = f.Damage<VoidbringersTouch>().Single();
        double stored = 0.10 * f.DamageBetween(before: eruption.Time).Sum(hit => hit.Value);

        using var check = new Checks(f);
        check.Equal(1.0, eruption.CritChance);
        check.Ratio(stored * 2 * balancing, eruption.Value, what: "the eruption, over the 10% stored");
    }

    /// <summary>
    /// Voidbringer's Touch stores the damage you deal to any enemy, not only to the one it is on.
    ///
    /// The same rule as above, with the damage dealt to a second enemy.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 1.1)]
    [InlineData("Elarion", "Focused Shot", 1.08)]
    [InlineData("Gunde", "Double Strike", 1.0)]
    [InlineData("Mara", "Widow's Bite", 1.04)]
    [InlineData("Tariq", "Skull Crusher", 1.0)]
    public void Voidbringers_Touch_Stores_Damage_Dealt_To_Other_Enemies(string hero, string ability, double balancing)
    {
        var f = StepFights.Of(hero, enemies: 2, weaponAbilityId: VoidbringersTouch.AbilityId).WithoutDamageSpread();
        f.FillResources(); // enough to cast it

        f.Cast<VoidbringersTouch>(target: f.Enemies()[0]);
        f.Cast(ability, target: f.Enemies()[1]);
        f.Wait(20); // the touch erupts

        var eruption = f.Damage<VoidbringersTouch>().Single();
        double stored = 0.10 * f.DamageBetween(before: eruption.Time).Sum(hit => hit.Value);

        using var check = new Checks(f);
        check.True(f.Damage(ability).All(hit => hit.Target == f.Enemies()[1].Name));
        check.True(eruption.Target == f.Enemies()[0].Name);
        check.Ratio(stored * 2 * balancing, eruption.Value, what: "the eruption, over the 10% stored");
    }

    /// <summary>
    /// Voidbringer's Touch erupts as soon as it holds 4250% of your main stat, before its 15 seconds are up.
    ///
    /// 10000% Expertise makes each hit about 100 times as large, so a few casts reach the cap.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Widow's Bite")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Voidbringers_Touch_Erupts_As_Soon_As_It_Holds_4250_Percent_Main_Stat(string hero, string ability)
    {
        var f = StepFights.Of(hero, expertise: 100, weaponAbilityId: VoidbringersTouch.AbilityId);

        f.Cast<VoidbringersTouch>();
        double start = f.CompletedAt("Voidbringer's Touch");
        for (int cast = 0; cast < 50 && f.Damage<VoidbringersTouch>().Count == 0; cast++)
        {
            f.FillResources(); // enough to cast it
            f.Cast(ability);
        }

        Assert.True(f.Damage<VoidbringersTouch>().Single().Time.TotalSeconds < start + 15 - 0.1);
    }

    /// <summary>
    /// An eruption at the cap deals 4250% of your main stat as a critical strike (x2), raised by your Expertise and your class's balancing
    /// aura like any other hit.
    ///
    /// 10000% Expertise, as above.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 1.1)]
    [InlineData("Elarion", "Focused Shot", 1.08)]
    [InlineData("Gunde", "Double Strike", 1.0)]
    [InlineData("Mara", "Widow's Bite", 1.04)]
    [InlineData("Tariq", "Skull Crusher", 1.0)]
    public void An_Eruption_At_The_Cap_Deals_4250_Percent_Main_Stat_Raised_By_Expertise(string hero, string ability, double balancing)
    {
        var f = StepFights.Of(hero, expertise: 100, weaponAbilityId: VoidbringersTouch.AbilityId).WithoutDamageSpread();

        f.Cast<VoidbringersTouch>();
        for (int cast = 0; cast < 50 && f.Damage<VoidbringersTouch>().Count == 0; cast++)
        {
            f.FillResources(); // enough to cast it
            f.Cast(ability);
        }

        double eruption = f.Damage<VoidbringersTouch>().Single().Value;

        AssertRatio(42.5 * 2 * (1 + 100) * balancing, eruption / f.MainStat());
    }

    /// <summary>
    /// Voidbringer's Touch erupts at once when the damage it holds is more than its enemy's health left, as that damage is dealt.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt")]
    [InlineData("Ardeos", "Infernal Wave")]
    [InlineData("Elarion", "Focused Shot")]
    [InlineData("Gunde", "Double Strike")]
    [InlineData("Mara", "Widow's Bite")]
    [InlineData("Tariq", "Skull Crusher")]
    public void Voidbringers_Touch_Erupts_At_Once_When_It_Holds_More_Than_Its_Enemys_Health(string hero, string ability)
    {
        var f = StepFights.Of(hero, enemies: 2, weaponAbilityId: VoidbringersTouch.AbilityId);
        f.FillResources(); // enough to cast it

        f.Cast<VoidbringersTouch>(target: f.Enemies()[0]);
        f.SetHealth(1e-8, on: f.Enemies()[0]); // nearly dead: 1 health left
        f.Cast(ability, target: f.Enemies()[1]);
        f.Wait(1); // the hit lands

        var eruption = f.Damage<VoidbringersTouch>().Single();

        using var check = new Checks(f);
        check.True(eruption.Target == f.Enemies()[0].Name);
        check.Seconds(f.Damage(ability)[0].Time.TotalSeconds, eruption.Time.TotalSeconds);
    }
}
