using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 397: each tick of Freezing Torrent grants a stack of Frostwyrm's Spite up to a maximum, and you can move
/// while channeling. Cold Snap consumes all stacks of Frostwyrm's Spite, increasing its damage per stack, and hits one
/// additional enemy near your target for each stack it consumes. Frostwyrm's Spite damage is reduced beyond a target count.
/// The extra hits are copies of that Cold Snap with all its bonuses but no Winter Orb (per the user), logged as effect
/// 2265. Combat logs (PCaMVTFbZzNLrfx3 fight 5, 21 casts): the main hit takes the full bonus; each extra hit deals
/// a share of it that falls off with N, the extra enemies hit beyond the target count.
/// </summary>
public partial class FrostwyrmsSpite : Legendary
{
    public const int BuffEffectId = 2264;
    public const int DamageEffectId = 2265;
    public const string DamageName = "Frostwyrm's Spite";

    public static readonly AuraDef Buff = AuraDef.Effect("Frostwyrm's Spite", BuffEffectId, BuffDuration, MaxNumStacks);

    public override void Bind(Hero hero)
    {
        if (hero is not Rime rime) return;
        var coldSnap = rime.GetAbility<ColdSnap>();
        if (coldSnap == null) return;
        int consumed = 0;

        hero.OnChannelTick += (ability, _, _, ctx) =>
        {
            if (ability is FreezingTorrent) hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
        // Cold Snap consumes every stack for more damage...
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != coldSnap) return;
            consumed = hero.Auras.GetStacks(Buff, cast.Time);
            if (consumed == 0) return;
            hero.Auras.Remove(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier(consumed);
        };
        // ...and copies its hit onto one more enemy near the target per stack.
        hero.OnCastCompleted += (ability, target, _) =>
        {
            if (ability != coldSnap || consumed == 0 || target == null || hero.SimContext is not { } ctx) return;
            int stacks = consumed;
            consumed = 0;
            var selected = ctx.Outcomes.SelectTargets(LogIds.Effect(DamageEffectId), ctx.ActiveEnemiesExcept(target), ctx);
            int struck = Math.Min(stacks, selected.Count);
            double threshold = TargetCountThreshold;
            double falloff = struck > threshold ? Math.Sqrt(threshold / struck) : 1.0;
            for (int i = 0; i < struck; i++)
            {
                coldSnap.DealDamage(ctx, selected[i], damageMultiplier: DamageMultiplier(stacks) * falloff,
                    overrideName: DamageName, overrideId: LogIds.Effect(DamageEffectId));
            }
        };
    }

    public static double DamageMultiplier(int stacks) => 1.0 + InstantSingleDamageDamagePerStack * stacks;
}
