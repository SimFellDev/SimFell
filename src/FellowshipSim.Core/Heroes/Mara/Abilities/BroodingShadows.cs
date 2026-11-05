namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1130: "Instantly enter Stealth ... When used in combat, Brooding Shadows triggers its cooldown. Can be used
/// during Global Cooldown. Brooding Shadows gains Cooldown Acceleration equal to your Haste." 2 charges, 15s. Stealth is
/// effect 1591; the next Backstab, Widow's Bite or Skittering Blades ends it and applies a poison.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 125 Stealths): no auto attack in Stealth; Kick and Stalker Step keep it.
/// The cast spends no charge: `GA_Mara_Stealth` has `CooldownCommitmentPolicy` CCP_Manual, and under it `CommitExecute`
/// (0x1450DCB70) applies cost alone. `GA_Mara_Passive_StealthMonitor` function `Remove Stealth Effect` spends the charge
/// when it ends Stealth (offset 231, `Commit Cooldown` offset 85); a Secondary skill's end of Stealth spends one in combat
/// alone (`ExecuteUbergraph` offset 1777, `IsInCombat()`).
/// A cast in Stealth ends it and does nothing else: `GA_Mara_Stealth` offsets 103 to 266, no cooldown node, no application.
/// Combat logs (the 23 Mara fights of data/fights): all 12 casts in Stealth log the removal in the cast's frame.
/// </summary>
public class BroodingShadows : MaraAbility
{
    public const int AbilityId = 1130;
    public const string AbilityName = "Brooding Shadows";
    public const double CooldownSeconds = 15.0;
    public const int Charges = 2;
    public const int StealthEffectId = 1591;

    /// <summary>
    /// Seconds no cast of the ability is possible after one, divided by the Haste at the cast: table AC_Hero_Mara
    /// `Mara.Stealth.PaddingCooldown`, `BlockedByPaddingCooldown` true, comment "To protect against double presses",
    /// applied after `CommitAbility` (0x1450DA320). `GE_Hero_Shared_PaddingCooldown` takes `GEC_HasteMagnitude` (1 / Haste,
    /// snapshot), multiplied by the padding (0x1450E2F8A-0x1450E2FA0), and has no time-rate tag, so it runs in real time.
    /// </summary>
    public const double PaddingCooldownSeconds = 0.7;

    public static readonly AuraDef Stealth = AuraDef.Effect("Brooding Shadows", StealthEffectId);

    private SimTime _paddingEndsAt;

    public BroodingShadows(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
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
        _paddingEndsAt = now + SimTime.FromSeconds(PaddingCooldownSeconds / (1.0 + Mara.GetHaste(now)));
        if (Mara.IsStealthed(now)) Mara.Auras.Remove(Stealth, now);
        else Mara.Auras.Apply(Stealth, now);
    }

    /// <summary>A Secondary skill ended Stealth: the charge goes in combat alone.</summary>
    public void EndedBySecondarySkill(SimulationContext ctx)
    {
        if (ctx.ActiveEnemies.Count > 0) TriggerCooldown(ctx);
    }
}
