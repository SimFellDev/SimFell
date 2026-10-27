using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional.GearSigil;

/// <summary>What the sigil tests share: building a hero with one sigil, forcing its procs and reading its buffs.</summary>
internal static class SigilFights
{
    /// <summary>Haste, expertise and crit rating each count 0.16% per point (below the 10% diminishing-returns breakpoint).</summary>
    public const double PercentPerRating = 0.0016;

    public static StepDriver Fight(GearHero hero, int sigil, int rank, double haste = 0, double crit = 0, double spirit = 0, int enemies = 1,
        int weapon = 0, double mainStat = 1000) =>
        hero.Fight(new GearLoadout { Augmentations = [(sigil, rank)], Haste = haste, Crit = crit, Spirit = spirit, Enemies = enemies, WeaponAbilityId = weapon, MainStat = mainStat });

    /// <summary>A hero with no character sheet percentages, so its stats come from ratings (the sheet already includes a sigil's rating).</summary>
    public static StepDriver FightWithoutSheet(GearHero hero, int sigil, int rank)
    {
        var build = new CharacterBuild { Name = "Test " + hero.Name, Hero = hero.Name, PrimaryStat = 1000 };
        if (sigil != 0) build.Augmentations.Add(new BuildEntry(sigil, rank));
        return new StepDriver(HeroImporter.Import(build), 1, 1);
    }

    /// <summary>
    /// Every roll of <paramref name="key"/> succeeds, up to <paramref name="times"/> of them (then fails); every other proc fails, so nothing but the
    /// sigil's own dice is in play.
    /// </summary>
    public static ForcedProcs Force(this StepDriver f, string key, int times = int.MaxValue)
    {
        int left = times;
        return f.ForceProcs(k => k == key ? left-- > 0 : false);
    }

    /// <summary>Every roll succeeds or fails as <paramref name="on"/> says now (for tests that switch it between casts).</summary>
    public static ForcedProcs Force(this StepDriver f, string key, Func<bool> on) => f.ForceProcs(k => k == key ? on() : false);

    /// <summary>Crits roll (instead of the expected value), so a hero with 100% crit crits every hit. Call before <see cref="Force"/>.</summary>
    public static void CritsRoll(this StepDriver f) => f.RandomOutcomes().Mode = OutcomeMode.Discrete;

    /// <summary>
    /// Casts the hero's Basic ability, a few milliseconds later if it is still on its cooldown (Wild Swing's is as long as its global cooldown,
    /// and a hasted cooldown can end just after the hero is free).
    /// </summary>
    public static void CastBasic(this StepDriver f, GearHero hero, EnemyTarget? target = null)
    {
        GearFights.Fill(f);
        var ability = hero.Get(f, hero.Basic);
        for (int tries = 0; ; tries++)
        {
            var attempt = f.TryCast(ability, target);
            if (attempt.Succeeded) return;
            if (tries >= 20) throw new InvalidOperationException($"Cannot cast {ability.Name} at {f.Now.TotalSeconds:F3}s: {attempt.Reason}");
            f.Wait(0.01);
        }
    }

    /// <summary>
    /// Casts an ability that can crit, as often as needed: the hero's Basic ability, except for Tariq, whose Wild Swing cannot crit in the engine (Face
    /// Breaker instead, waiting out its cooldown).
    /// </summary>
    public static void CastCrittable(this StepDriver f, GearHero hero)
    {
        GearFights.Fill(f);
        var ability = hero.Name == "Tariq" ? f.Hero.Abilities.First(a => a.Name == "Face Breaker") : hero.Get(f, hero.Basic);
        for (int tries = 0; ; tries++)
        {
            var attempt = f.TryCast(ability);
            if (attempt.Succeeded) return;
            if (tries >= 400) throw new InvalidOperationException($"Cannot cast {ability.Name} at {f.Now.TotalSeconds:F3}s: {attempt.Reason}");
            f.Wait(0.05);
        }
    }

    /// <summary>
    /// The class damage tuning every hero's damage carries (the game's BalancingDamageAura): Rime +4%, Ardeos +10%, Elarion +8%, Mara +4%, Gunde and
    /// Tariq none. A gear effect that deals damage on its own is scaled by it like any other hit.
    /// </summary>
    public static double ClassTuning(GearHero hero) => hero.Name switch
    {
        "Rime" => 1.04,
        "Ardeos" => 1.10,
        "Elarion" => 1.08,
        "Mara" => 1.04,
        _ => 1.0,
    };

    /// <summary>The seconds of every gain of an aura, in order.</summary>
    public static List<double> GainedAt(this StepDriver f, string aura) =>
        f.Events.Where(e => e.Kind == StepEventKind.AuraGained && e.Name == aura).Select(e => e.Time.TotalSeconds).ToList();

    public static List<double> RemovedAt(this StepDriver f, string aura) =>
        f.Events.Where(e => e.Kind == StepEventKind.AuraRemoved && e.Name == aura).Select(e => e.Time.TotalSeconds).ToList();

    public static double Haste(this StepDriver f) => f.Hero.GetHaste(f.Now);
    public static double MainStat(this StepDriver f) => f.Hero.PrimaryAttribute(f.Now);

    /// <summary>The time the hero's last cast completed.</summary>
    public static double LastCastCompletedAt(this StepDriver f) =>
        f.Events.Last(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;

    /// <summary>One theory row per hero and rank, with the rank's value from <paramref name="valueByRank"/> (ranks 1 to 4).</summary>
    public static IEnumerable<object[]> HeroesByRank(params double[] valueByRank) =>
        GearFights.Heroes.SelectMany(h => valueByRank.Select((v, i) => new object[] { h, i + 1, v }));

    public static IEnumerable<object[]> HeroesByRank(double[] first, double[] second) =>
        GearFights.Heroes.SelectMany(h => first.Select((v, i) => new object[] { h, i + 1, v, second[i] }));
}
