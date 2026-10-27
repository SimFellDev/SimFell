using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using WA = FellowshipSim.Core.Customization.WeaponAbilities;

namespace FellowshipSim.Tests.Functional.GearWeapon;

/// <summary>Which heroes can have which weapon ability, as theory data, from each hero's own list in <see cref="GearHero.WeaponAbilities"/>.</summary>
public static class WeaponHeroes
{
    public const int FatedStrikeId = 160;
    public const int SahrilsWrathId = 157;
    public const int VoidbringersTouchId = 155;
    public const int ChronoshiftId = 1558;
    public const int IciclesId = 1561;
    public const int NaturesFuryId = 161;

    private static IEnumerable<object[]> For(int weaponId) =>
        GearFights.Heroes.Where(h => h.WeaponAbilities.Contains(weaponId)).Select(h => new object[] { h });

    public static IEnumerable<object[]> FatedStrike => For(FatedStrikeId);
    public static IEnumerable<object[]> SahrilsWrath => For(SahrilsWrathId);
    public static IEnumerable<object[]> VoidbringersTouch => For(VoidbringersTouchId);
    public static IEnumerable<object[]> Chronoshift => For(ChronoshiftId);
    public static IEnumerable<object[]> Icicles => For(IciclesId);
    public static IEnumerable<object[]> NaturesFury => For(NaturesFuryId);

    /// <summary>Every hero with every weapon ability it can have: (hero, weapon ability ID, weapon ability name).</summary>
    public static IEnumerable<object[]> Pairs =>
        GearFights.Heroes.SelectMany(h => h.WeaponAbilities.Select(id => new object[] { h, id, NameOf(id) }));

    public static string NameOf(int weaponId) => weaponId switch
    {
        FatedStrikeId => "Fated Strike",
        SahrilsWrathId => "Sahril's Wrath",
        VoidbringersTouchId => "Voidbringer's Touch",
        ChronoshiftId => "Chronoshift",
        IciclesId => "Icicles of An'zhyr",
        NaturesFuryId => "Nature's Fury",
        _ => throw new ArgumentOutOfRangeException(nameof(weaponId)),
    };
}

/// <summary>What the weapon tests share: a hero with a weapon, and the units its damage is read in.</summary>
internal static class WeaponFights
{
    /// <summary>The Main Stat every weapon test hero has.</summary>
    public const double MainStat = 1000;

    /// <summary>A bare hero wearing the weapon with ability <paramref name="weaponId"/>.</summary>
    public static StepDriver WithWeapon(this GearHero hero, int weaponId, double haste = 0, int enemies = 1, double crit = 0, double expertise = 0, GearLoadout? gear = null) =>
        hero.Fight(new GearLoadout
        {
            WeaponAbilityId = weaponId, Haste = haste, Enemies = enemies, Crit = crit, Expertise = expertise, MainStat = MainStat,
            Glyphs = gear?.Glyphs ?? [], Augmentations = gear?.Augmentations ?? [], Spirit = gear?.Spirit ?? 0,
        });

    public static Ability Weapon(this StepDriver f) => f.Hero.WeaponAbility ?? throw new InvalidOperationException("no weapon");

    /// <summary>
    /// The hero's class damage tuning (the game scales every ability of a class by one factor: 1.04 for Rime, 1.1 for Ardeos...). A weapon
    /// ability's damage is its coefficient x Main Stat x this.
    /// </summary>
    public static double Tuning(this StepDriver f) => f.Hero.DamageScale(f.Now);

    /// <summary>A damage amount as a multiple of Main Stat, without the class tuning (the coefficient the tooltip gives).</summary>
    public static double Coefficient(this StepDriver f, double damage) => damage / (MainStat * f.Tuning());

    /// <summary>
    /// The cooldown of <paramref name="ability"/> in cooldown seconds the moment the cast of <paramref name="cast"/> completes (the ability itself by
    /// default), before anything the cast gives has had time to speed it up. The hero reports the time left at the current recovery speed, so it is
    /// multiplied back by the speed.
    /// </summary>
    public static Func<double> CooldownAtCompletion(this StepDriver f, Ability ability, Ability? cast = null)
    {
        double remaining = double.NaN;
        cast ??= ability;
        f.Hero.OnCastCompleted += (a, _, _) =>
        {
            if (a == cast) remaining = f.CooldownSecondsLeft(ability);
        };
        return () => remaining;
    }

    /// <summary>The cooldown left in cooldown seconds: the hero reports the time left at the current recovery speed, so it is multiplied back.</summary>
    public static double CooldownSecondsLeft(this StepDriver f, Ability ability) =>
        f.Hero.GetCooldownRemains(ability.Name, f.Now) * f.Hero.CooldownRate(ability, f.Now);

    /// <summary>The time the aura named <paramref name="name"/> was gained, in order.</summary>
    public static List<double> GainedAt(this StepDriver f, string name) =>
        f.Events.Where(e => e.Kind == StepEventKind.AuraGained && e.Name == name).Select(e => e.Time.TotalSeconds).ToList();

    /// <summary>The time the aura named <paramref name="name"/> was removed, in order.</summary>
    public static List<double> RemovedAt(this StepDriver f, string name) =>
        f.Events.Where(e => e.Kind == StepEventKind.AuraRemoved && e.Name == name).Select(e => e.Time.TotalSeconds).ToList();

    /// <summary>Whether the hero has the aura named <paramref name="name"/> now.</summary>
    public static bool IsUp(this StepDriver f, string name) =>
        f.Hero.Auras.Active(f.Now).Any(a => a.Def.Name == name);

    /// <summary>Lets time pass until the fight clock reads <paramref name="seconds"/>.</summary>
    public static void WaitUntil(this StepDriver f, double seconds) => f.Wait(seconds - f.Now.TotalSeconds);

    /// <summary>When the cast of <paramref name="name"/> completed.</summary>
    public static double CompletedAt(this StepDriver f, string name) =>
        f.Events.First(e => e.Kind == StepEventKind.CastCompleted && e.Name == name).Time.TotalSeconds;

    /// <summary>
    /// Starts an instant ability of the hero (its first Basic, Core or Power probe without a cast time) and returns with its global cooldown still running,
    /// which <see cref="StepDriver.Cast(Ability, EnemyTarget?)"/> does not (it waits for the hero to be free).
    /// </summary>
    public static void StartGlobalCooldown(this StepDriver f, GearHero hero)
    {
        GearFights.Fill(f);
        var ability = new[] { hero.Basic, hero.Core, hero.Power }.Select(t => hero.Get(f, t)).First(a => a.CastTime == SimTime.Zero && !a.IgnoresGcd && !a.IsChanneled);
        var attempt = f.Context.BeginCast(f.Hero, ability, f.Enemy);
        if (!attempt.Succeeded) throw new InvalidOperationException($"{ability.Name}: {attempt.Reason}");
        f.Wait(0.05);
        if (f.IsFree) throw new InvalidOperationException($"{ability.Name} left no global cooldown");
    }

    /// <summary>The hero's own unit hit at the start of a fight.</summary>
    public static void CastUnit(this StepDriver f, GearHero hero) => hero.CastProbe(f, hero.Unit);
}
