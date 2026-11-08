namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1106, a Kick Technique finisher (Wind Kick then Earth Fist): "Perform a roundhouse kick, dealing 232.05% of
/// Agility damage to your target and nearby enemies. Lashing Stormkick heals you for 50% of the damage it deals. AFFECTED
/// BY STORMFALL: Lashing Stormkick deals +100% increased damage when it can consume a stack of Stormfall. Can be used
/// during Global Cooldown". Logged under 1106. Its damage falls off beyond <see cref="TargetThresholdForDamageScale"/>
/// enemies (game data targetCountFalloff of effect 1537).
/// </summary>
public partial class LashingStormkick : MeikoFinisher
{
    /// <summary>"+100% increased damage when it can consume a stack of Stormfall."</summary>
    public const double StormfallMultiplier = 2.0;

    /// <summary>
    /// A kick struck: its damage multiplier, the enemies it struck and whether it consumed Stormfall (Perfect Storm,
    /// Whipping Apex).
    /// </summary>
    public event Action<SimulationContext, EnemyTarget, double, int, bool>? Kicked;

    public LashingStormkick(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory, Technique.Kick, Technique.Fist)
    {
        BaseCoefficient = AgilityCoefficient;
        AoeTargetThreshold = TargetThresholdForDamageScale;
        Hit += (in _) => _hits++;
    }

    // The kick's hits landed so far (Whipping Apex counts the enemies a kick struck).
    private int _hits;

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        bool stormfall = Meiko.ConsumeCharge(RisingStorm.Stormfall, ctx);
        double multiplier = stormfall ? StormfallMultiplier : 1.0;
        int before = _hits;
        Kick(ctx, target, multiplier);
        Kicked?.Invoke(ctx, target, multiplier, _hits - before, stormfall);
    }

    /// <summary>
    /// One kick on <paramref name="target"/> and the enemies near it, under <paramref name="damageId"/> (the ability's own
    /// ID by default).
    /// </summary>
    public void Kick(SimulationContext ctx, EnemyTarget target, double damageMultiplier, int? damageId = null, string? name = null) =>
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies, damageMultiplier, overrideId: damageId, overrideName: name);
}
