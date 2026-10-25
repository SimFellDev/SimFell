namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2291: "Cleave all enemies in front of you, dealing 270.92% of Strength and granting you Serrated Edge for 10
/// seconds, causing your next damaging ability to transfer an additional 20% of its damage to Rend. Serrated Edge does
/// not interact with your Bloodbound Spirit ability. Blood Arc applies Rend to the targets for 50% of the damage dealt."
/// Damage falls off as sqrt(8/N) beyond 8 enemies (game data effect 3236). Cooldown 9s, shortened by Haste at the cast.
/// </summary>
public class BloodArc : GundeAbility
{
    public const int AbilityId = 2291;
    public const string AbilityName = "Blood Arc";
    public const double Coefficient = 2.7092;
    public const double CooldownSeconds = 9.0;
    public const double TargetThreshold = 8;
    public const double RendShareOfDamage = 0.5;

    public const int SerratedEdgeEffectId = 3232;
    /// <summary>Combat logs: every Serrated Edge lasts 8s (the tooltip says 10s; game data effect 3232 says 8).</summary>
    public const double SerratedEdgeSeconds = 8.0;
    public const double SerratedEdgeShare = 0.2;

    /// <summary>
    /// "Your next damaging ability converts an additional +20% of the damage dealt into Rend." Combat logs (Y4wpb2f3gc8LqJDX
    /// fight 25, 157 Serrated Edges): used up by the next Heart Splitter, Rupture, Reaver's Edge or Grim Carve; Owed in
    /// Blood, Reign in Blood, Slaughter, Fated Strike, Warbound and Jawbreaker leave it.
    /// </summary>
    public static readonly AuraDef SerratedEdge = AuraDef.Effect("Serrated Edge", SerratedEdgeEffectId, SerratedEdgeSeconds);

    /// <summary>Blood Arc's hits have gone off, with its target (Crimson Strikes, Death's Arc).</summary>
    public event Action<SimulationContext, EnemyTarget>? Arced;

    public BloodArc(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        RendShare = RendShareOfDamage;
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
