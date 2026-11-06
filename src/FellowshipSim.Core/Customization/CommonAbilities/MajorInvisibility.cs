namespace FellowshipSim.Core.Customization.CommonAbilities;

/// <summary>
/// Ability 1486 (a relic ability): conceals the caster and nearby allies with Major Invisibility. It can only be used out of
/// combat. Effect 2146 "Major Invisibility": "This effect is removed if you generate threat or deal any damage to
/// enemies." (game data; the cooldown and the effect's duration are <see cref="CooldownSeconds"/> and <see cref="BuffSeconds"/>).
/// No damage. UNVERIFIED: whether it starts the global cooldown.
/// </summary>
public class MajorInvisibility : Ability
{
    public const int AbilityId = 1486;
    public const string AbilityName = "Major Invisibility";
    public const double CooldownSeconds = 360.0;
    public const int BuffEffectId = 2146;
    public const double BuffSeconds = 15.0;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public MajorInvisibility(Hero owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Relic)
    {
        SetCooldown(CooldownSeconds);
        // Dealing damage to an enemy ends it.
        owner.OnDamageDealt += (in dmg) =>
        {
            if (!_cast || dmg.Target == null || dmg.Amount <= 0) return;
            _cast = false;
            Owner.Auras.Remove(Buff, dmg.Timestamp);
        };
    }

    /// <summary>Cast since the last hit dealt (the buff may be up).</summary>
    private bool _cast;

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
        _cast = true;
        TriggerCooldown(ctx);
    }
}
