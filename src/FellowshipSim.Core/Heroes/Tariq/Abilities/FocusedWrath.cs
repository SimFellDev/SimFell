namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1233: "Your next casts of Skull Crusher or Hammer Storm cost less Fury and deal more damage. Can
/// be used during Global Cooldown." Effect 1769 (`BuffSeconds`, up to `MaxStacks` stacks).
/// </summary>
public class FocusedWrath : TariqAbility
{
    public const int AbilityId = 1233;
    public const string AbilityName = "Focused Wrath";
    /// <summary>Game data cooldown, scaled by cooldown reduction and recovery only.</summary>
    public const double CooldownSeconds = 90.0;
    public const int BuffEffectId = 1769;
    public const double BuffSeconds = 15.0;
    public const int MaxStacks = 3;
    public const int StacksGranted = 2;
    public const double CostMultiplier = 0.48;
    /// <summary>Culling Strike tooltip value.</summary>
    public const double BuffDamageMultiplier = 1.1;

    public static readonly AuraDef Buff = AuraDef.Effect("Focused Wrath", BuffEffectId, BuffSeconds, MaxStacks);

    public FocusedWrath(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime, stacks: StacksGranted);
    }

    /// <summary>What a Skull Crusher or Hammer Storm of <paramref name="baseCost"/> Fury costs now.</summary>
    public static double Cost(Tariq hero, double baseCost, SimTime now) =>
        hero.Auras.HasAura(Buff, now) ? baseCost * CostMultiplier : baseCost;

    /// <summary>
    /// Pays for a Skull Crusher or Hammer Storm of <paramref name="baseCost"/> Fury, using up a stack if there is one;
    /// returns the damage multiplier the stack gives.
    /// </summary>
    public static double Spend(Tariq hero, double baseCost, SimulationContext ctx)
    {
        bool focused = hero.Auras.HasAura(Buff, ctx.CurrentTime);
        hero.SpendFury(focused ? baseCost * CostMultiplier : baseCost, ctx);
        if (!focused) return 1.0;
        hero.Auras.ConsumeStack(Buff, ctx.CurrentTime);
        return BuffDamageMultiplier;
    }
}
