namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2291: cleaves all enemies in front of Gunde and grants Serrated Edge, which makes the next damaging ability
/// transfer additional damage to Rend. Serrated Edge does not interact with the Bloodbound Spirit ability. Blood Arc
/// applies Rend to the targets. Damage falls off beyond <see cref="Constants.DamageScalingTargetCountThreshold"/> enemies (game data effect 3236).
/// The cooldown is shortened by Haste at the cast.
/// </summary>
public partial class BloodArc : GundeAbility
{

    public const int SerratedEdgeEffectId = 3232;
    /// <summary>Combat logs: every Serrated Edge lasts the duration of game data effect 3232, not the tooltip's.</summary>
    public const double SerratedEdgeSeconds = 8.0;
    public const double SerratedEdgeShare = 0.2;

    /// <summary>
    /// "Your next damaging ability converts an additional share of the damage dealt into Rend." Combat logs (Y4wpb2f3gc8LqJDX
    /// fight 25, 157 Serrated Edges): used up by the next Heart Splitter, Rupture, Reaver's Edge or Grim Carve; Owed in
    /// Blood, Reign in Blood, Slaughter, Fated Strike, Warbound and Jawbreaker leave it.
    /// </summary>
    public static readonly AuraDef SerratedEdge = AuraDef.Effect("Serrated Edge", SerratedEdgeEffectId, SerratedEdgeSeconds);

    /// <summary>Blood Arc's hits have gone off, with its target (Crimson Strikes, Death's Arc).</summary>
    public event Action<SimulationContext, EnemyTarget>? Arced;

    public BloodArc(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        AoeTargetThreshold = Constants.DamageScalingTargetCountThreshold;
        RendShare = Constants.PerTargetAccumulatedBleedDamageToDotScaler;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var rend = BeginRend(ctx);
        RendingPulse(ctx, rend, AreaTargets(ctx, target));
        Gunde.Auras.Apply(SerratedEdge, ctx.CurrentTime);
        Arced?.Invoke(ctx, target);
    }
}
