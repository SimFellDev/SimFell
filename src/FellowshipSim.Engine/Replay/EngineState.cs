namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The engine's state at a <see cref="ReplayVerifier"/> check: the hero, and for checks with them the target, the hit and
/// the cast.
/// </summary>
public sealed record EngineState(double Time, EngineHero Hero, EngineTarget? Target, EngineHit? Hit, EngineCast? Cast)
{
    /// <summary>The state at <paramref name="ctx"/>'s current time.</summary>
    /// <param name="hit">The hit of a hit or tick check: its numbers at the check, with its release and landing times.</param>
    /// <param name="cast">The ability of a cast check, with the reason <see cref="Ability.CanCast(SimulationContext, out string?)"/> gives.</param>
    public static EngineState Capture(SimulationContext ctx, EnemyTarget? target, in HitRequest? hit = null, double? releasedAt = null,
        double? landedAt = null, Ability? cast = null, string? refusal = null)
    {
        var now = ctx.CurrentTime;
        var hero = ctx.Player;
        var heroState = new EngineHero(
            hero.PrimaryAttribute(now),
            hero.CritChance(now),
            hero.CritChanceAgainst(target, now),
            hero.CritMultiplier(now),
            hero.GetHaste(now),
            hero.Expertise(now),
            hero.SpiritPercent(now),
            hero.DamageScale(now),
            hero.ResourceDefs.Select(r => new EngineResource(r.Name, hero.Resources.Get(r).Current, hero.Resources.Get(r).Max)).ToArray(),
            hero.GlobalCooldownEndTime.TotalSeconds,
            hero.CurrentCast?.Ability.Name,
            hero.CurrentChannel?.Ability.Name,
            Auras(hero.Auras, now),
            ctx.ActiveEnemies.Count);

        EngineTarget? targetState = target == null ? null : new EngineTarget(
            target.Id, target.InstanceId, target.Name, target.HealthFraction, Auras(target.Auras, now),
            target.Auras.Fold(StatType.IncomingDamageMultiplier, 1.0, now));

        EngineHit? hitState = null;
        if (hit is { } h)
        {
            // The pending hit is the one being released or landed (a hit check runs inside ResolveHit or Land).
            var pending = ctx.CurrentHit is { } current && current.AbilityId == h.AbilityId && current.Target == h.Target ? current : null;
            hitState = new EngineHit(
                h.AbilityName, h.AbilityId, h.BaseDamage,
                pending?.Ability != null ? pending.DamageMultiplier : null,
                pending?.Ability is { } ability ? hero.AbilityDamageMultiplier(ability, now) : null,
                hero.DamageMultiplierAgainst(h.Target, now),
                h.CritChance, pending?.CastCritChance, pending?.ExtraCritChance, h.CritMultiplier, h.TickFraction, pending?.Snapshot,
                releasedAt ?? h.Time.TotalSeconds, landedAt);
        }

        EngineCast? castState = null;
        if (cast != null)
        {
            // An ability that never started its cooldown has no state; reading CooldownOf would make one.
            var cooldown = hero.Cooldowns.GetValueOrDefault(cast.Name);
            int charges = cooldown?.ChargesAt(now) ?? cast.MaxCharges;
            castState = new EngineCast(
                cast.Name,
                cast.GetAdjustedCastTime(now).TotalSeconds,
                cast.GlobalCooldownSeconds ?? hero.GlobalCooldownSeconds,
                cast.GlobalCooldownScalesWithHaste ?? hero.GlobalCooldownScalesWithHaste,
                cooldown == null || charges > 0 ? 0.0 : Math.Max(0.0, (cooldown.NextChargeAt - now).TotalSeconds),
                charges,
                hero.CooldownRate(cast, now),
                refusal);
        }

        return new EngineState(now.TotalSeconds, heroState, targetState, hitState, castState);
    }

    private static EngineAura[] Auras(AuraContainer auras, SimTime now) =>
        auras.Active(now).Select(a => new EngineAura(a.Name, a.LogId, a.Stacks,
            a.ExpiresAt == SimTime.Infinite ? null : (a.ExpiresAt - now).TotalSeconds, a.Modifiers)).ToArray();
}

/// <param name="CritChanceAgainstTarget"><see cref="Hero.CritChanceAgainst"/> the check's target (the hero's crit chance with no target).</param>
public sealed record EngineHero(
    double PrimaryAttribute,
    double CritChance,
    double CritChanceAgainstTarget,
    double CritMultiplier,
    double Haste,
    double Expertise,
    double Spirit,
    double DamageScale,
    IReadOnlyList<EngineResource> Resources,
    double GlobalCooldownEnd,
    string? CurrentCast,
    string? CurrentChannel,
    IReadOnlyList<EngineAura> Auras,
    int ActiveEnemies);

public sealed record EngineResource(string Name, double Current, double Max);

/// <param name="SecondsLeft">Null for an aura with no expiry.</param>
public sealed record EngineAura(string Name, int LogId, int Stacks, double? SecondsLeft, IReadOnlyList<StatModifier> Modifiers)
{
    /// <summary>Each modifier as <c>{aura} x{stacks}: {type} {op} {value}</c>.</summary>
    public IEnumerable<string> ModifierTexts =>
        Modifiers.Select(m => $"{Name} x{Stacks}: {m.Type} {m.Op} {m.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}");
}

public sealed record EngineTarget(int Id, int InstanceId, string Name, double HealthFraction, IReadOnlyList<EngineAura> Auras,
    double IncomingDamageMultiplier);

/// <param name="HitDamageMultiplier"><see cref="PendingHit.DamageMultiplier"/>; null for a hit with fixed or custom numbers.</param>
/// <param name="AbilityDamageMultiplier"><see cref="Hero.AbilityDamageMultiplier"/>; null for a hit with fixed or custom numbers.</param>
/// <param name="LandingTime">Null for a hit that never landed.</param>
public sealed record EngineHit(
    string AbilityName,
    int AbilityId,
    double BaseDamage,
    double? HitDamageMultiplier,
    double? AbilityDamageMultiplier,
    double TargetDamageMultiplier,
    double CritChance,
    double? CastCritChance,
    double? ExtraCritChance,
    double CritMultiplier,
    double TickFraction,
    bool? Snapshot,
    double ReleaseTime,
    double? LandingTime);

/// <param name="Refusal">The reason <see cref="Ability.CanCast(SimulationContext, out string?)"/> gives; null when it allows the cast.</param>
public sealed record EngineCast(
    string Ability,
    double CastTime,
    double GlobalCooldown,
    bool GlobalCooldownScalesWithHaste,
    double CooldownLeft,
    int Charges,
    double CooldownRate,
    string? Refusal);
