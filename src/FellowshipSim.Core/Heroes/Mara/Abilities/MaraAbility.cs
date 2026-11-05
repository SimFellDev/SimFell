namespace FellowshipSim.Core.Heroes.Mara.Abilities;

// Each ability lists its game values as constants at the top (IDs and numbers from the game data).

/// <summary>Base for Mara's abilities: the Energy cost, Stealth, Creeping Death and Combo Point generation.</summary>
public abstract class MaraAbility : Ability
{
    protected MaraAbility(Mara owner, int id, string name, AbilityCategory category = AbilityCategory.None)
        : base(owner, id, name, category) { }

    protected Mara Mara => (Mara)Owner;

    /// <summary>Energy the ability costs (game data "cost", resource Energy).</summary>
    public double EnergyCost { get; protected set; }

    /// <summary>Energy a cast spends less the Energy the cast gives.</summary>
    public virtual double NetEnergyCost => EnergyCost;

    /// <summary>Using the ability ends Stealth (every attack; Kick, Stalker Step and the cooldowns do not).</summary>
    protected bool BreaksStealth { get; set; }

    /// <summary>The ability is used from Stealth alone (the Stealth Backstab and Skittering Blades).</summary>
    protected bool RequiresStealth { get; set; }

    /// <summary>The ability cannot be used in Stealth (auto attacks; abilities whose Stealth version replaces them there).</summary>
    protected bool UnusableInStealth { get; set; }

    /// <summary>Creeping Death scales the ability's damage with Haste (poisons and bleeds).</summary>
    protected bool HasCreepingDeath { get; set; }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        var now = ctx.CurrentTime;
        if (RequiresStealth && !Mara.IsStealthed(now))
        {
            reason = explain ? $"{Name} needs Stealth" : null;
            return false;
        }
        if (UnusableInStealth && Mara.IsStealthed(now))
        {
            reason = explain ? $"{Name} cannot be used in Stealth" : null;
            return false;
        }
        if (EnergyCost > 0)
        {
            double energy = Mara.EnergyAt(now);
            if (energy < EnergyCost)
            {
                reason = explain ? $"{Name} needs {EnergyCost:0} Energy (have {energy:0.#})" : null;
                return false;
            }
        }
        return base.CanCast(ctx, explain, out reason);
    }

    /// <summary>Pays the Energy cost and ends Stealth: the first thing every Mara ability's cast does.</summary>
    protected void Pay(SimulationContext ctx)
    {
        _maidenAtCast = Mara.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime);
        if (EnergyCost > 0) Mara.SpendEnergy(EnergyCost, ctx);
        if (!BreaksStealth || !Mara.IsStealthed(ctx.CurrentTime)) return;
        Mara.Auras.Remove(BroodingShadows.Stealth, ctx.CurrentTime);
        Mara.RaiseAttackedFromStealth(ctx, this);
    }

    /// <summary>Multipliers on the ability's hits against one enemy (Arachnid Onslaught, Seething Poison, Gushing Blood).</summary>
    public List<Func<EnemyTarget, SimulationContext, double>> TargetMultipliers { get; } = new();

    public override double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0)
    {
        double damage = base.BaseDamage(ctx, target, damageMultiplier);
        if (HasCreepingDeath) damage *= Mara.CreepingDeath(ctx.CurrentTime);
        if (target != null)
        {
            foreach (var m in TargetMultipliers) damage *= m(target, ctx);
        }
        return damage;
    }

    /// <summary>
    /// A generator's Combo Points from a hit: <paramref name="points"/>, or a full refill when Maiden of Death is active at the
    /// cast ("all combo point generating abilities generate 6 Combo Points"). Each generator's `EventReceived` callback on
    /// `Event.Damage` calls `BFL_Mara_Utils.MaraUtils_GainComboPoints` (`GA_Mara_AoeAttackBuilder` offsets 1908 to 2302), so a
    /// cast with no hit generates none: combat logs have 33 Skittering Blades with no hit over the 8,255 generator casts in 23
    /// Mara fights, 7 of them inside Maiden of Death, and every one leaves Combo Points unchanged (T2qLPpmMkrAtJ9B1 fight 18 @
    /// 1139.361, xkGbdm8aNDwTcgjF fight 17 @ 289.281). A hit in the cast's server frame reads the frame's Maiden of Death:
    /// v8rhNxVc3PwDqzBA fight 4 player 28 @ 183.304 has the hit 1ms and the buff's end 4ms after the cast, and 6 Combo Points.
    /// </summary>
    protected void GenerateComboPoints(SimulationContext ctx, double points, EnemyTarget? from) =>
        Mara.GainComboPoints(_maidenAtCast ? MaidenOfDeath.ComboPointsPerGenerator : points, ctx, from);

    private bool _maidenAtCast;
}
