namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1130: "Instantly enter Stealth ... When used in combat, Brooding Shadows triggers its cooldown. Can be used
/// during Global Cooldown. Brooding Shadows gains Cooldown Acceleration equal to your Haste." Stealth is
/// effect 1591; the next Backstab, Widow's Bite or Skittering Blades ends it and applies a poison.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 125 Stealths): no auto attack in Stealth; Kick and Stalker Step keep it.
/// The cast spends no charge: game data sets a manual cooldown commitment, and under it the commit
/// applies cost alone. The Stealth monitor spends the charge
/// when it ends Stealth; a Secondary skill's end of Stealth spends one in combat
/// alone.
/// A cast in Stealth ends it and does nothing else: the ability has no cooldown node and no application there.
/// Combat logs (the 23 Mara fights of data/fights): all 12 casts in Stealth log the removal in the cast's frame.
/// </summary>
public partial class BroodingShadows : MaraAbility
{
    public const int StealthEffectId = 1591;

    public static readonly AuraDef Stealth = AuraDef.Effect("Brooding Shadows", StealthEffectId);

    /// <summary>
    /// End of the padding cooldown: no cast of the ability is possible for <see cref="PaddingCooldown"/> seconds after one,
    /// divided by the Haste at the cast. Game data: blocked by padding cooldown true, comment "To protect against double
    /// presses", applied after the ability commits. The padding effect takes 1 / Haste (snapshot), multiplied by the
    /// padding, and has no time-rate tag, so it runs in real time.
    /// </summary>
    private SimTime _paddingEndsAt;

    public BroodingShadows(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override AuraDef? RecastEnds => Stealth;

    public void ResetState() => _paddingEndsAt = SimTime.Zero;

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (ctx.CurrentTime < _paddingEndsAt)
        {
            reason = explain ? $"{Name} in its padding cooldown ({(_paddingEndsAt - ctx.CurrentTime).TotalSeconds:F2}s remaining)" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override SimTime NextCastChange(SimulationContext ctx) =>
        ctx.CurrentTime < _paddingEndsAt ? _paddingEndsAt : base.NextCastChange(ctx);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        _paddingEndsAt = now + SimTime.FromSeconds(PaddingCooldown / (1.0 + Mara.GetHaste(now)));
        if (Mara.IsStealthed(now)) Mara.Auras.Remove(Stealth, now);
        else Mara.Auras.Apply(Stealth, now);
    }

    /// <summary>A Secondary skill ended Stealth: the charge goes in combat alone.</summary>
    public void EndedBySecondarySkill(SimulationContext ctx)
    {
        if (ctx.ActiveEnemies.Count > 0) TriggerCooldown(ctx);
    }
}
