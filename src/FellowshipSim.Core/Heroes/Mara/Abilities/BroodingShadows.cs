namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1130: "Instantly enter Stealth ... When used in combat, Brooding Shadows triggers its cooldown. Can be used
/// during Global Cooldown. Brooding Shadows gains Cooldown Acceleration equal to your Haste." 2 charges, 15s. Stealth is
/// effect 1591; the next Backstab, Widow's Bite or Skittering Blades ends it and applies a poison.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 125 Stealths): no auto attack in Stealth; Kick and Stalker Step keep it.
/// </summary>
public class BroodingShadows : MaraAbility
{
    public const int AbilityId = 1130;
    public const string AbilityName = "Brooding Shadows";
    public const double CooldownSeconds = 15.0;
    public const int Charges = 2;
    public const int StealthEffectId = 1591;

    public static readonly AuraDef Stealth = AuraDef.Effect("Brooding Shadows", StealthEffectId);

    public BroodingShadows(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Mara.Auras.Apply(Stealth, ctx.CurrentTime);
    }
}
