using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1313: "Fire a Highwind Arrow, dealing physical damage to target enemy. Highwind Arrow Ricochets to
/// additional enemies near your target, dealing physical damage to them. Highwind Arrow gains Cooldown Acceleration equal
/// to your Haste." Has a cast time, costs Focus and recharges its charges (cooldownTimeRateScalesOff Haste).
/// Combat logs (fXyzwGcQkTrLD34R fight 4): each ricochet strikes another enemy a few server frames after the hit before
/// it, for a share of the first hit (<see cref="BounceDamageMultiplierAfterFirstHit"/>), with the cast's bonuses and
/// crit chance (a Lethal Shots cast's ricochets are grievous crits too).
/// </summary>
public partial class HighwindArrow : ElarionAbility
{
    public const double Cost = 30.0;
    public const int BaseRicochets = 2;

    /// <summary>Talent 126 Resurgent Winds: the buffed Highwind Arrow deals more damage.</summary>
    public const double ResurgentWindsMultiplier = 1.5;

    /// <summary>Resurgent Winds is taken: its buff makes the next Highwind Arrow instant, free and stronger.</summary>
    public bool HasResurgentWinds { get; set; }

    /// <summary>Ricochets of the cast resolving now (Final Crescendo raises them for one cast); read once by the cast.</summary>
    public int CastRicochets { get; set; } = BaseRicochets;

    /// <summary>A Highwind Arrow was cast (Rising Moon, Event Horizon).</summary>
    public event Action<SimulationContext>? Cast;

    /// <summary>A cast's ricochets that found no enemy, once its last arrow is done (Striker's Aim).</summary>
    public event Action<SimulationContext, int>? RicochetsMissed;

    public HighwindArrow(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = AgilityCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
        FocusCost = Cost;
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
    }

    private bool ResurgentWindsUp(SimTime now) => HasResurgentWinds && Owner.Auras.HasAura(ResurgentWinds.Buff, now);

    public override SimTime GetAdjustedCastTime(SimTime now = default) =>
        ResurgentWindsUp(now) ? SimTime.Zero : base.GetAdjustedCastTime(now);

    public override double CostAt(SimTime now) => ResurgentWindsUp(now) ? 0.0 : base.CostAt(now);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        TriggerCooldown(ctx);
        double multiplier = 1.0;
        if (ResurgentWindsUp(now))
        {
            Owner.Auras.ConsumeStack(ResurgentWinds.Buff, now);
            multiplier = ResurgentWindsMultiplier;
        }
        else
        {
            Pay(ctx, CostAt(now));
        }
        int ricochets = CastRicochets;
        CastRicochets = BaseRicochets;
        var (castDamage, castCrit) = CastBonuses;
        var struck = new HashSet<EnemyTarget>();
        if (target != null) struck.Add(target);

        void Ricochet(SimulationContext c, int left)
        {
            var candidates = c.ActiveEnemies.Where(e => !struck.Contains(e)).ToList();
            var next = candidates.Count == 0 ? null : c.Outcomes.ChooseTarget(DamageLogId, candidates, c);
            if (next == null || !candidates.Contains(next))
            {
                RicochetsMissed?.Invoke(c, left);
                return;
            }
            struck.Add(next);
            DealDamage(c, next, damageMultiplier: castDamage * multiplier * BounceDamageMultiplierAfterFirstHit, extraCritChance: castCrit,
                onLanded: (cc, _) => After(cc, left - 1));
        }

        void After(SimulationContext c, int left)
        {
            if (left <= 0)
            {
                RicochetsMissed?.Invoke(c, 0);
                return;
            }
            // The ricochet leaves as the hit before it lands; a replay takes its flight from the log.
            Ricochet(c, left);
        }

        if (target != null) DealDamage(ctx, target, damageMultiplier: multiplier, onLanded: (c, _) => After(c, ricochets));
        Cast?.Invoke(ctx);
    }
}
