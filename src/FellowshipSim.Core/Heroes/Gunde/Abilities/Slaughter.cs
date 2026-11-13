namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2292 / effect 3237: "Strike with immense power around you, consuming all Rend stacks on enemies hit and
/// applying Slaughter to them. Slaughter is a bleed that deals a multiple of the total Rend damage consumed from each enemy, to
/// each enemy over a set time." The cooldown is not hasted.
/// Combat logs (n7zxK8harZT2wjDB fight 9 @ 28.655, Y4wpb2f3gc8LqJDX fight 25): each enemy's Slaughter is
/// <see cref="Constants.DotAccDotToNewDotScaler"/> times the damage its own Rend had still to deal (Desecrator); it ticks at
/// <see cref="Constants.DotPeriod"/> from the first tick after the cast, never crits and takes no damage multipliers from Gunde. It
/// takes no global cooldown (Gunde guide corpus).
/// Each tick takes its target's damage-taken multipliers, as Rend's tick does. Game data (build 26320967): the bleed
/// (GE_Gunde_InstantAoeDot_Dot) has the preset GundeRend of DT_EffectCalculationPresets, with
/// ApplyTargetIncomingDamageHealingMultipliers true, and the damage execution multiplies by the target's
/// IncomingDamageMultiplier at each hit and each tick of an effect with that flag (binary, gather 0x1450DB180, formula
/// 0x145107B15). Combat logs (patch 0.5.0.0, the log's tick over the exact replay's
/// tick without the multiplier): 4.000 on an enemy taking 4 times the damage (medians; CwmX2Tb8GA6hyc1v fight 8, 124
/// ticks; Ab1yCxVcNBHPz6t8 fight 7, 69 ticks; 67qwcRKCWYFzyxPA fight 6, 32 ticks; 250 ticks in 6 fights), 1.100 and
/// 1.300 at 1.1 and 1.3 times (DrabZB8wYcQ4fx6m fight 33, 67 and 46 ticks), 0.199 at 0.2 times (CwmX2Tb8GA6hyc1v fight
/// 8, 3 ticks), and 0.996 on the other enemies (13,976 ticks in the 20 fights).
/// </summary>
public partial class Slaughter : GundeAbility
{
    public const int DotEffectId = 3237;

    public static readonly AuraDef Dot = AuraDef.Effect(AbilityName, DotEffectId, Constants.DotDuration);

    /// <summary>
    /// Slaughter consumed Rend: the sum of the Rend stacks the enemies had before the consume (Massacre; Blueprint
    /// InstantAoeDot's <c>TotalAccumulatedStacks</c>, build 26320967).
    /// </summary>
    public event Action<SimulationContext, int>? Consumed;

    public Slaughter(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        DamageLogId = LogIds.Effect(DotEffectId);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var rend = Gunde.Rend;
        // The enemies around Gunde with Rend on them (a replay follows the log's first ticks).
        var withRend = ctx.ActiveEnemies.Where(rend.IsOn).ToList();
        if (withRend.Count == 0) return;
        int stacks = 0;
        foreach (var enemy in ctx.Outcomes.SelectTargets(DamageLogId, withRend, ctx))
        {
            if (!rend.IsOn(enemy)) continue;
            stacks += enemy.Auras.GetStacks(Rend.Debuff, ctx.CurrentTime);
            double consumed = rend.Consume(ctx, enemy);
            double multiplier = Constants.DotAccDotToNewDotScaler;
            if (enemy.Auras.HasAura(Rupture.OpenWounds, ctx.CurrentTime))
            {
                multiplier *= Rupture.OpenWoundsSlaughterDamage;
                enemy.Auras.Remove(Rupture.OpenWounds, ctx.CurrentTime);
            }
            Bleed(ctx, enemy, consumed * multiplier);
        }
        if (stacks > 0) Consumed?.Invoke(ctx, stacks);
    }

    /// <summary>Deals <paramref name="damage"/> to <paramref name="enemy"/> over its ticks.</summary>
    private void Bleed(SimulationContext ctx, EnemyTarget enemy, double damage)
    {
        if (damage <= 0.0) return;
        enemy.Auras.Apply(Dot, ctx.CurrentTime);
        int ticks = (int)Math.Round(Constants.DotDuration / Constants.DotPeriod);
        double perTick = damage / ticks;
        int fired = 0;
        PeriodicEffect.Begin(ctx, Name, Constants.DotDuration, Constants.DotPeriod, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) =>
            {
                fired++;
                if (enemy.IsSleeping) return;
                c.ApplyDamage(Owner, enemy, perTick * Gunde.DamageMultiplierAgainst(enemy, c.CurrentTime), DamageLogId, Name, canCrit: false, 0.0, 1.0);
                Gunde.CheckSunderedFlesh(c, enemy, Math.Max(0, ticks - fired) * perTick);
            },
            onEnd: (c, _) => enemy.Auras.Remove(Dot, c.CurrentTime),
            damageLogId: DamageLogId, target: enemy);
    }
}
