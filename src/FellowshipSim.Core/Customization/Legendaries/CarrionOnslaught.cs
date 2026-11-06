using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Gunde's Northlander's Wraps (item 5900): Carrion Onslaught. The game data's item names talent 765 and no legendary power
/// (power 713 is "Placeholder - Not Implemented"), so the text is its effects': 3256 raises the damage of the next Blood Arc
/// by a base and by an amount per stack consumed, and makes it start dropping Blood Feathers, one per set number of stacks
/// consumed; 3257 drops Blood Feathers at a set interval. Combat logs (cnjfpzmD7ZXakMFH fight 33, TTV TheSpryFrog):
/// <list type="bullet">
/// <item>Owed in Blood's cast grants effect 3256 (one stack) in the moment it consumes its stacks;</item>
/// <item>the next Blood Arc swaps it for effect 3257 with a stack per feather, a share of the Owed in Blood stacks
/// consumed, rounded; it drops a batch at once and a batch at each interval until none are left (the
/// text's batch size is out of date; FellowSimc's SimC module has the log's);</item>
/// <item>the Blood Arc deals its damage times a base plus an amount per Owed in Blood stack consumed: the replay's Blood Arc
/// damage is close to the log's with it and far below the log's without it.</item>
/// </list>
/// </summary>
public class CarrionOnslaught : Legendary
{
    /// <summary>The game data's item has no legendary power of its own.</summary>
    public const int PowerId = 0;
    public const int WrapsItemId = 5900;
    public const string EffectName = "Carrion Onslaught";
    public const int BuffEffectId = 3256;
    public const int FeathersEffectId = 3257;
    public const double EffectSeconds = 12.0;
    public const double DamageMultiplier = 1.2;
    public const double DamagePerStack = 0.02;
    public const int StacksPerFeather = 3;
    public const int FeathersPerTick = 10;
    public const double TickSeconds = 2.0;

    /// <summary>The next Blood Arc's bonus.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect(EffectName, BuffEffectId, EffectSeconds);

    /// <summary>The Blood Feathers still to drop, a stack each.</summary>
    public static readonly AuraDef Feathers = AuraDef.Effect(EffectName, FeathersEffectId, EffectSeconds, OwedInBlood.MaxStacks);

    public override int LegendaryPowerId => PowerId;
    public override string Name => EffectName;
    public override string HeroName => "Gunde";
    public override IReadOnlyList<int> ItemIds => [WrapsItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Gunde gunde || gunde.GetAbility<OwedInBlood>() is not { } owedInBlood || gunde.GetAbility<BloodArc>() is not { } bloodArc) return;
        // The Owed in Blood stacks the bonus holds, and those the Blood Arc casting consumes.
        int held = 0, consumed = 0;

        owedInBlood.Spent += (ctx, _, stacks) =>
        {
            held = stacks;
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
        // The next Blood Arc deals more for every stack...
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != bloodArc || !hero.Auras.HasAura(Buff, cast.Time)) return;
            consumed = held;
            hero.Auras.Remove(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier + DamagePerStack * consumed;
        };
        // ...and starts dropping Blood Feathers.
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (ability != bloodArc || consumed == 0 || hero.SimContext is not { } ctx) return;
            int feathers = (int)Math.Round((double)consumed / StacksPerFeather);
            consumed = 0;
            if (feathers == 0) return;
            hero.Auras.Apply(Feathers, ctx.CurrentTime, setStacks: Math.Min(feathers, OwedInBlood.MaxStacks));
            PeriodicEffect.Begin(ctx, EffectName, EffectSeconds, TickSeconds, tickOnStart: true, partialFinalTick: false,
                onTick: (c, _) =>
                {
                    int left = hero.Auras.GetStacks(Feathers, c.CurrentTime);
                    if (left == 0) return;
                    int lost = Math.Min(FeathersPerTick, left);
                    gunde.CollectFeathers(c, lost);
                    if (lost == left) hero.Auras.Remove(Feathers, c.CurrentTime);
                    else hero.Auras.Apply(Feathers, c.CurrentTime, setStacks: left - lost);
                },
                onEnd: (c, _) => hero.Auras.Remove(Feathers, c.CurrentTime));
        };
    }
}
