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
    /// A generator's Combo Points: <paramref name="points"/>, or 6 for the whole cast while Maiden of Death is active
    /// ("all combo point generating abilities generate 6 Combo Points").
    /// </summary>
    protected void GenerateComboPoints(SimulationContext ctx, double points, EnemyTarget? from)
    {
        if (Mara.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime)) return;
        Mara.GainComboPoints(points, ctx, from);
    }

    /// <summary>Maiden of Death gives a generator's cast its 6 Combo Points at once.</summary>
    protected bool GenerateMaidenComboPoints(SimulationContext ctx, EnemyTarget? from)
    {
        if (!Mara.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime)) return false;
        Mara.GainComboPoints(MaidenOfDeath.ComboPointsPerGenerator, ctx, from);
        return true;
    }
}
