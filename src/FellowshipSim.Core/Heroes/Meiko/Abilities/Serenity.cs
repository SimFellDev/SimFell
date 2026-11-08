namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1119: "Instantly heal yourself for 220% of Agility. Each time you Auto Attack an enemy, your Serenity gains +40%
/// increased healing, stacking up to 50 times. Can be used during Global Cooldown". 10 second cooldown, its duration
/// scaled by Haste (cooldownDurationScalesOff Haste). The stacks it consumes count for Everlasting Spirit.
/// </summary>
public partial class Serenity : MeikoAbility
{
    /// <summary>
    /// Serenity's stacks: auto attacks, Earthwell, Whipping Apex and Warden of the Temple add to them. In the game they are
    /// effect 1566 (HealBonusTracker, maxStacks 50); combat logs never show it.
    /// </summary>
    public static readonly AuraDef Stacks = AuraDef.Internal("Serenity Stacks", 0, HealMaxStacks);

    /// <summary>Serenity was cast, with the stacks it consumed (Everlasting Spirit).</summary>
    public event Action<SimulationContext, int>? Soothed;

    public Serenity(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        int stacks = Owner.Auras.GetStacks(Stacks, now);
        Owner.Auras.Remove(Stacks, now);
        Soothed?.Invoke(ctx, stacks);
    }
}
