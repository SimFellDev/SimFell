namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1237: "Conjure an arc of lightning onto target enemy that chains between you and other enemies until it has
/// dealt damage a total of 6 times. Chain Lightning does not deal damage to you. Thunder Call: You can only cast Chain
/// Lightning while Thunder Call is active. Generates 1.2 Fury per hit." Its hits are effect 1774.
/// </summary>
public class ChainLightning : TariqAbility
{
    public const int AbilityId = 1237;
    public const string AbilityName = "Chain Lightning";
    /// <summary>Placeholder ChainLightDmg: 149.98% of Strength per hit.</summary>
    public const double Coefficient = 1.4998;
    /// <summary>Game data cooldown 8, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 8.0;
    public const int Hits = 6;
    /// <summary>Ink.BouncyProjectile.ResourceGainPerTarget: 1.2% of maximum Fury per hit.</summary>
    public const double FuryPerHit = 1.2;
    public const int DamageEffectId = 1774;

    /// <summary>
    /// Combat logs (fXyzwGcQkTrLD34R fight 4, 58 casts): the first hit 0.30s after the cast, then a hit every 0.05s
    /// (ability 1236 period 0.05; the log rounds each to 1 or 2 server frames).
    /// </summary>
    public const double FirstHitSeconds = 0.30;
    public const double BounceSeconds = 0.05;

    /// <summary>Hits added to each cast (Thunderstruck).</summary>
    public int ExtraHits { get; internal set; }

    /// <summary>Multiplier on the Fury each hit generates (Thundering Vortex).</summary>
    public double FuryMultiplier { get; internal set; } = 1.0;

    /// <summary>Damage added for each unique enemy other than the target the cast has hit (The Motherload).</summary>
    public double DamagePerOtherEnemy { get; internal set; }

    /// <summary>The arc bounced to its next hit (Thundering Vortex).</summary>
    public event Action<SimulationContext>? Bounced;

    public ChainLightning(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    /// <summary>Ace of Spades: "Your Chain Lightning now has 2 charges" (they recharge with haste, as native charges do).</summary>
    internal void SetCharges(int charges) => SetCooldown(CooldownSeconds, charges, scalesWithHaste: true);

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (!Tariq.IsCharged(ctx.CurrentTime))
        {
            reason = explain ? $"{Name} needs Thunder Call" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Launch(ctx, target);
    }

    /// <summary>A free Chain Lightning at <paramref name="target"/> from a talent's proc (<paramref name="source"/>): no cooldown.</summary>
    public void FreeCast(SimulationContext ctx, EnemyTarget target, object source) =>
        ResolveCast(ctx, target, source, () => Launch(ctx, target));

    /// <summary>Releases the arc: its hits keep the damage bonuses and crit the cast had.</summary>
    private void Launch(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Tariq;
        double castMultiplier = CastDamageMultiplier;
        double castCrit = (CurrentCast?.CritChance ?? 0.0) + hero.LightningCritChance;
        int hits = Hits + ExtraHits;
        var hitSoFar = new HashSet<EnemyTarget>();
        EnemyTarget? last = null;
        for (int i = 0; i < hits; i++)
        {
            bool first = i == 0;
            ctx.Schedule(SimTime.FromSeconds(FirstHitSeconds + i * BounceSeconds), c =>
            {
                var next = NextTarget(c, target, first, last, hitSoFar);
                if (next == null) return;
                if (!first) Bounced?.Invoke(c);
                last = next;
                if (next != target) hitSoFar.Add(next);
                double motherload = 1.0 + DamagePerOtherEnemy * hitSoFar.Count;
                hero.GainFury(FuryPerHit * FuryMultiplier, c, next);
                DealDamage(c, next, damageMultiplier: castMultiplier * motherload, extraCritChance: castCrit);
            }, Name);
        }
    }

    /// <summary>
    /// The enemy the arc hits next: the target first, then one it has not hit yet, else any but the last (with one enemy
    /// the arc hits it again, from Tariq). A replay follows the log.
    /// </summary>
    private EnemyTarget? NextTarget(SimulationContext ctx, EnemyTarget target, bool first, EnemyTarget? last, HashSet<EnemyTarget> hitSoFar)
    {
        var enemies = ctx.ActiveEnemies;
        if (enemies.Count == 0) return null;
        EnemyTarget? preferred = first && target is { IsSleeping: false } ? target : null;
        for (int i = 0; preferred == null && i < enemies.Count; i++)
        {
            if (enemies[i] != target && enemies[i] != last && !hitSoFar.Contains(enemies[i])) preferred = enemies[i];
        }
        preferred ??= enemies.FirstOrDefault(e => e != last) ?? enemies[0];
        return ctx.Outcomes.ChooseTarget(DamageLogId, enemies, ctx, preferred);
    }
}
