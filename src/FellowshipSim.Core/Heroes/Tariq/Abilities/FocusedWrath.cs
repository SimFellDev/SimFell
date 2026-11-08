namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1233: "Your next casts of Skull Crusher or Hammer Storm cost less Fury and deal more damage. Can
/// be used during Global Cooldown." Effect 1769 (`AbilityDuration`, up to `MaxStacksLimit` stacks).
/// </summary>
public partial class FocusedWrath : TariqAbility
{
    public const int BuffEffectId = 1769;

    public static readonly AuraDef Buff = AuraDef.Effect("Focused Wrath", BuffEffectId, AbilityDuration, MaxStacksLimit);

    public FocusedWrath(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        // Scaled by cooldown reduction and recovery only.
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime, stacks: NumOfStacks);
    }

    /// <summary>What a Skull Crusher or Hammer Storm of <paramref name="baseCost"/> Fury costs now.</summary>
    public static double Cost(Tariq hero, double baseCost, SimTime now) =>
        hero.Auras.HasAura(Buff, now) ? baseCost * CostReduction : baseCost;

    /// <summary>
    /// Pays for a Skull Crusher or Hammer Storm of <paramref name="baseCost"/> Fury, using up a stack if there is one;
    /// returns the damage multiplier the stack gives.
    /// </summary>
    public static double Spend(Tariq hero, double baseCost, SimulationContext ctx)
    {
        bool focused = hero.Auras.HasAura(Buff, ctx.CurrentTime);
        hero.SpendFury(focused ? baseCost * CostReduction : baseCost, ctx);
        if (!focused) return 1.0;
        hero.Auras.ConsumeStack(Buff, ctx.CurrentTime);
        return DamageIncrease;
    }
}
