namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1859: "Sunlight envelopes the ground at your feet, burning enemies within its area for magical damage every 1.5
/// seconds over 12 seconds. You take 20% reduced damage while standing in your Shining Halo. Can be used during Global
/// Cooldown. BRILLIANCE: While Shining Halo is active, each Brilliant Flash you cast extends the duration of it by 2
/// seconds." Cooldown 30s, 218 Mana.
/// </summary>
public class ShiningHalo : XavianAbility
{
    public const int AbilityId = 1859;
    public const string AbilityName = "Shining Halo";
    /// <summary>Placeholder ShiningHaloDmg: 27% of Strength.</summary>
    public const double Coefficient = 0.27;
    public const double CooldownSeconds = 30.0;
    /// <summary>Game data cost 218 Mana.</summary>
    public const double Cost = 218;
    public const double DurationSeconds = 12.0;
    /// <summary>
    /// Combat logs (XyGpcHjNaAzgk7mW fight 20, 24 halos): a pulse as it is cast, then one every 1.50s whatever the Haste.
    /// </summary>
    public const double PulseSeconds = 1.5;
    /// <summary>Game data targetCountFalloff threshold 12.</summary>
    public const double TargetThreshold = 12;
    /// <summary>BRILLIANCE: seconds each Brilliant Flash adds while the halo is active.</summary>
    public const double BrillianceSeconds = 2.0;

    /// <summary>
    /// Effect 2569: up while Xavian stands in the halo, so combat logs remove and apply it as he moves.
    /// </summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, 2569);

    /// <summary>Seconds added to the halo's duration (Grand Halo).</summary>
    public double BonusDurationSeconds { get; set; }

    /// <summary>Multipliers on the next halo's pulses, read as it is cast (Light's Oath).</summary>
    public List<Func<SimulationContext, double>> CastDamageMultipliers { get; } = new();

    /// <summary>Raised as a halo is cast (Light's Oath).</summary>
    public event Action<SimulationContext>? Placed;

    private SimTime _endsAt = SimTime.Zero;
    private int _halo;

    public ShiningHalo(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Power)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        ManaCost = Cost;
        IgnoresGcd = true;
        AoeTargetThreshold = TargetThreshold;
    }

    public bool IsActive(SimTime now) => now <= _endsAt && _halo > 0;

    public override void Connect()
    {
        Xavian.BrilliantFlash.Flashed += (ctx, _) => Extend(ctx, BrillianceSeconds);
    }

    /// <summary>Adds <paramref name="seconds"/> to an active halo (Brilliance, Zenith Technique).</summary>
    public void Extend(SimulationContext ctx, double seconds)
    {
        if (IsActive(ctx.CurrentTime)) _endsAt += SimTime.FromSeconds(seconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        PayMana(ctx);
        var now = ctx.CurrentTime;
        double multiplier = 1.0;
        foreach (var castMultiplier in CastDamageMultipliers) multiplier *= castMultiplier(ctx);
        _snapshot = new SimulationContext.HitNumbers(BaseDamage(ctx, null, multiplier), Owner.CritChance(now) + CritChanceBonus,
            CritMultiplierAt(now));
        Placed?.Invoke(ctx);
        // A new halo replaces the one up (combat logs: no pulse of the old one after the cast).
        int halo = ++_halo;
        _endsAt = now + SimTime.FromSeconds(DurationSeconds + BonusDurationSeconds);
        Xavian.Auras.Apply(Buff, now);
        Pulse(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);

        void Next(SimulationContext c)
        {
            if (halo != _halo) return;
            if (c.CurrentTime > _endsAt)
            {
                c.Player.Auras.Remove(Buff, c.CurrentTime);
                return;
            }
            Pulse(c);
            c.Schedule(SimTime.FromSeconds(PulseSeconds), Next, AbilityName);
        }
        ctx.Schedule(SimTime.FromSeconds(PulseSeconds), Next, AbilityName);
    }

    /// <summary>
    /// One pulse on every enemy, with the numbers the halo took as it was cast. Combat logs (XyGpcHjNaAzgk7mW fight 20 @
    /// 15.099 and 39.519): pulses keep the damage of the cast's moment, buffs gained or lost since aside.
    /// </summary>
    private void Pulse(SimulationContext ctx)
    {
        if (ctx.ActiveEnemies.Count == 0) return;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, ctx.ActiveEnemies, ctx);
        double falloff = Falloff(struck.Count, TargetThreshold);
        var now = ctx.CurrentTime;
        for (int i = 0; i < struck.Count; i++)
        {
            var enemy = struck[i];
            ctx.ApplyDamage(Owner, enemy, _snapshot.BaseDamage * falloff * Owner.DamageMultiplierAgainst(enemy, now), DamageLogId, Name,
                canCrit: true, _snapshot.CritChance, _snapshot.CritMultiplier);
        }
    }

    private SimulationContext.HitNumbers _snapshot;

    public void ResetState()
    {
        _endsAt = SimTime.Zero;
        _halo = 0;
        _snapshot = default;
    }
}
