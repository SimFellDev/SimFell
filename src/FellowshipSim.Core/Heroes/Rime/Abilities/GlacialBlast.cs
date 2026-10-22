using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1028. Costs 2 Winter Orbs. Talon Strike (Icy Talons) is built on it.</summary>
public class GlacialBlast : RimeAbility
{
    public const int AbilityId = 1028;
    public const string AbilityName = "Glacial Blast";
    public const double Coefficient = 10.24;
    public const double CastTimeSeconds = 2.0;
    public const int DefaultWinterOrbCost = 2;

    /// <summary>Talent 38 Glacial Assault: "deals 40% more damage" at 4 stacks.</summary>
    public const double GlacialAssaultMultiplier = 1.40;
    /// <summary>Talent 38: "dealing 30% of the initial damage to nearby enemies" (ability 2023's text says 20%).</summary>
    public const double GlacialAssaultExplosionFraction = 0.30;
    public const int GlacialAssaultExplosionId = 2023;
    /// <summary>
    /// Combat logs: the explosion is 30% of the hit actually dealt (crit included), never crits itself, and loses damage
    /// as sqrt(12/N) beyond 12 targets (yMAZVJL9hTpFjqbC fight 7 @ 31.8s: explosion/spike = 0.2598 on 16, 0.2520 on 17).
    /// </summary>
    public const double GlacialAssaultExplosionTargetThreshold = 12;

    /// <summary>Seconds added to the cast time (Greater Glacial Blast).</summary>
    public double ExtraCastTimeSeconds { get; set; }

    /// <summary>Haste added to the cast while casting (Icy Flow); read when the cast starts.</summary>
    public List<Func<SimTime, double>> CastHasteBonuses { get; } = new();

    public GlacialBlast(Rime owner) : this(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
    }

    /// <summary>For abilities built on Glacial Blast (Talon Strike), which set their own values.</summary>
    protected GlacialBlast(Rime owner, int id, string name) : base(owner, id, name, AbilityCategory.Power) { }

    protected bool GlacialAssaultReady => Rime.GlacialAssaultStacks >= 4;

    public override SimTime GetAdjustedCastTime(SimTime now = default)
    {
        // Instant with 4 Glacial Assault stacks or during Wrath of Winter.
        if (GlacialAssaultReady || Rime.Auras.HasAura(WrathOfWinter.Buff, now)) return SimTime.Zero;
        double seconds = CastTime.TotalSeconds + ExtraCastTimeSeconds;
        double castHaste = CastHasteBonuses.Sum(bonus => bonus(now));
        return SimTime.FromSeconds(seconds / (1.0 + Owner.GetHaste(now) + castHaste));
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason) =>
        MeetsRequirements(explain, out reason) && base.CanCast(ctx, explain, out reason);

    /// <summary>Talent and Winter Orb requirements, checked before the cooldown.</summary>
    protected virtual bool MeetsRequirements(bool explain, out string? reason)
    {
        if (Rime.HasIcyTalons)
        {
            reason = explain ? $"{Name} is replaced by Talon Strike (Icy Talons)" : null;
            return false;
        }
        if (Rime.WinterOrbs < DefaultWinterOrbCost && !GlacialAssaultReady)
        {
            reason = explain ? $"{Name} needs {DefaultWinterOrbCost} Winter Orbs (have {Rime.WinterOrbs})" : null;
            return false;
        }
        reason = null;
        return true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Rime;
        bool assault = GlacialAssaultReady;
        double multiplier = 1.0;
        if (assault) multiplier = ConsumeGlacialAssault(ctx);
        else hero.SpendWinterOrbs(DefaultWinterOrbCost, ctx);

        DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: assault ? (c, dealt) => Explode(c, target, dealt) : null);
    }

    /// <summary>Removes the 4 Glacial Assault stacks; returns the damage multiplier they give.</summary>
    protected double ConsumeGlacialAssault(SimulationContext ctx)
    {
        Rime.Auras.Remove(GlacialAssault.Buff, ctx.CurrentTime);
        return GlacialAssaultMultiplier;
    }

    /// <summary>Called when a Glacial Assault hit on <paramref name="target"/> lands, with the damage it dealt.</summary>
    protected void Explode(SimulationContext ctx, EnemyTarget target, double initialDamage)
    {
        if (initialDamage <= 0.0) return;
        var nearby = ctx.Outcomes.SelectTargets(GlacialAssaultExplosionId, ctx.ActiveEnemies.Where(e => e != target).ToList(), ctx);
        if (nearby.Count == 0) return;
        double falloff = nearby.Count > GlacialAssaultExplosionTargetThreshold ? Math.Sqrt(GlacialAssaultExplosionTargetThreshold / nearby.Count) : 1.0;
        double damage = initialDamage * GlacialAssaultExplosionFraction * falloff;
        foreach (var enemy in nearby)
        {
            ctx.ApplyDamage(Owner, enemy, damage, GlacialAssaultExplosionId, "Glacial Assault", canCrit: false, 0.0, 1.0);
        }
    }
}
