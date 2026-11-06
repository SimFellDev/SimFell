namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2292 / effect 3237: "Strike with immense power around you, consuming all Rend stacks on enemies hit and
/// applying Slaughter to them. Slaughter is a bleed that deals a multiple of the total Rend damage consumed from each enemy, to
/// each enemy over a set time." The cooldown is not hasted.
/// Combat logs (n7zxK8harZT2wjDB fight 9 @ 28.655, Y4wpb2f3gc8LqJDX fight 25): each enemy's Slaughter is
/// <see cref="RendMultiplier"/> times the damage its own Rend had still to deal (Desecrator); it ticks at
/// <see cref="TickSeconds"/> from the first tick after the cast, never crits and takes no damage multipliers. It takes no
/// global cooldown (Gunde guide corpus).
/// </summary>
public class Slaughter : GundeAbility
{
    public const int AbilityId = 2292;
    public const string AbilityName = "Slaughter";
    public const double CooldownSeconds = 30.0;
    public const int DotEffectId = 3237;
    public const double RendMultiplier = 1.6;
    public const double DotSeconds = 3.0;
    public const double TickSeconds = 0.3;

    public static readonly AuraDef Dot = AuraDef.Effect(AbilityName, DotEffectId, DotSeconds);

    /// <summary>Slaughter consumed an enemy's Rend (Massacre).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Consumed;

    public Slaughter(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
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
        foreach (var enemy in ctx.Outcomes.SelectTargets(DamageLogId, withRend, ctx))
        {
            if (!rend.IsOn(enemy)) continue;
            double consumed = rend.Consume(ctx, enemy);
            double multiplier = RendMultiplier;
            if (enemy.Auras.HasAura(Rupture.OpenWounds, ctx.CurrentTime))
            {
                multiplier *= Rupture.OpenWoundsSlaughterDamage;
                enemy.Auras.Remove(Rupture.OpenWounds, ctx.CurrentTime);
            }
            Consumed?.Invoke(ctx, enemy, consumed);
            Bleed(ctx, enemy, consumed * multiplier);
        }
    }

    /// <summary>Deals <paramref name="damage"/> to <paramref name="enemy"/> over its ticks.</summary>
    private void Bleed(SimulationContext ctx, EnemyTarget enemy, double damage)
    {
        if (damage <= 0.0) return;
        enemy.Auras.Apply(Dot, ctx.CurrentTime);
        int ticks = (int)Math.Round(DotSeconds / TickSeconds);
        double perTick = damage / ticks;
        PeriodicEffect.Begin(ctx, Name, DotSeconds, TickSeconds, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) =>
            {
                if (!enemy.IsSleeping) c.ApplyDamage(Owner, enemy, perTick, DamageLogId, Name, canCrit: false, 0.0, 1.0);
            },
            onEnd: (c, _) => enemy.Auras.Remove(Dot, c.CurrentTime),
            damageLogId: DamageLogId, target: enemy);
    }
}
