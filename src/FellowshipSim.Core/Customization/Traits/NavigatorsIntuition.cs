namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 43: "Your offensive abilities have a 20% chance to grant you 28 / 66 / 104 / 142 Critical Strike, Haste,
/// Expertise or Spirit rating for 30 seconds. Your highest secondary stat is always the one that gets increased.
/// Navigator's Intuition can trigger once every 90 seconds." (effects 2158 Spirit, 2159 Haste, 2160 Expertise, 2161
/// Critical Strike). The highest stat is read from the ratings. UNVERIFIED: no log has this trait.
/// </summary>
public class NavigatorsIntuition : Trait
{
    public const int TraitId = 43;
    public const string TraitName = "Navigator's Intuition";
    public const double ProcChance = 0.20;
    public const double BuffSeconds = 30.0;
    public const double InternalCooldownSeconds = 90.0;
    public const string ProcKeyPrefix = "navigators_intuition_";
    public static readonly double[] RatingByRank = [28, 66, 104, 142];

    /// <summary>The buff for each secondary stat: (stat, effect ID, name).</summary>
    public static readonly (StatType Rating, int EffectId, string Name)[] Buffs =
    [
        (StatType.SpiritRating, 2158, "Navigator's Intuition : Spirit"),
        (StatType.HasteRating, 2159, "Navigator's Intuition : Haste"),
        (StatType.ExpertiseRating, 2160, "Navigator's Intuition : Expertise"),
        (StatType.CritRating, 2161, "Navigator's Intuition : Critical Strike"),
    ];

    /// <summary>The proc's name for the stat it raises (each has its own buff in the log).</summary>
    public static string ProcKey(StatType rating) => ProcKeyPrefix + rating;

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        var stats = hero.Stats;
        var (rating, effectId, name) = Buffs.MaxBy(b => b.Rating switch
        {
            StatType.CritRating => stats.CritRating,
            StatType.HasteRating => stats.HasteRating,
            StatType.ExpertiseRating => stats.ExpertiseRating,
            _ => stats.SpiritRating,
        });
        var buff = hero.Register(AuraDef.Effect(name, effectId, BuffSeconds, modifiers: StatModifier.Add(rating, ForRank(RatingByRank))));
        var ready = SimTime.Zero;
        // The fight the internal cooldown belongs to (each sim iteration and replay has its own).
        SimulationContext? fight = null;
        hero.OnCastCompleted += (ability, target, now) =>
        {
            if (target == null || ability.BaseCoefficient <= 0 || hero.SimContext is not { } ctx) return;
            if (fight != ctx)
            {
                fight = ctx;
                ready = SimTime.Zero;
            }
            if (now < ready) return;
            if (!ctx.Outcomes.RollProc(ProcKey(rating), ProcChance, ctx)) return;
            ready = now + SimTime.FromSeconds(InternalCooldownSeconds);
            hero.Auras.Apply(buff, now);
        };
    }
}
