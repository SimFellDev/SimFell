using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Gunde's Northlander's Wraps (item 5900): Carrion Onslaught. The datamine's item names talent 765 and no legendary power
/// (power 713 is "Placeholder - Not Implemented"), so the text is its effects': 3256, "The damage of your next Blood Arc is
/// increased by 20% (DamageMultiplier 1.2), plus 2% increase per stack consumed (AdditionalDamagePerStack 0.02). Your next
/// Blood Arc will also start dropping Blood Feathers, one per third stack consumed (StacksPerOrb 3)", 12s; and 3257, "Up
/// to 3 Blood Feathers will be dropped every 2 seconds", 12s. Combat logs (cnjfpzmD7ZXakMFH fight 33, TTV TheSpryFrog):
/// <list type="bullet">
/// <item>Owed in Blood's cast grants effect 3256 (one stack) in the moment it consumes its stacks;</item>
/// <item>the next Blood Arc swaps it for effect 3257 with a stack per feather, a third of the Owed in Blood stacks
/// consumed, rounded (84 -> 28, 51 -> 17, 80 -> 27); it drops 10 at once and 10 more every 2s until none are left (the
/// text's 3 is out of date; FellowSimc's SimC module has 10).</item>
/// <item>the Blood Arc deals 1.2 + 0.02 per Owed in Blood stack consumed times its damage: the replay's Blood Arc damage is
/// 1.006 of the log's with it (32 hits it raises: 1.009), 0.866 without.</item>
/// </list>
/// </summary>
public class CarrionOnslaught : Legendary
{
    /// <summary>The datamine's item has no legendary power of its own.</summary>
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
