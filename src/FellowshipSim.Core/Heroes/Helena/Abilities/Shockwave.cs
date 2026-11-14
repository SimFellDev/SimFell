namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 979: "Slam sword and shield together creating a forceful explosion, dealing 383.3% of Strength physical damage
/// to enemies in a large radius. Enemies that take damage from Shockwave have reduced movement speed for 5 seconds.
/// Shockwave generates 25% Toughness. VETERAN OF WAR: Shockwave reduces the cooldown of your Shields Up ability by 6
/// seconds." Cooldown 30s, shortened by Haste at the cast; up to 20 enemies, losing damage as sqrt(5/N) beyond 5. The hits
/// are effect 1272 and the slow is effect 1270.
/// </summary>
public partial class Shockwave : HelenaAbility
{
    public const int DamageEffectId = 1272;
    public const int SlowEffectId = 1270;
    public const double SlowSeconds = 5.0;
    /// <summary>
    /// Toughness per cast, as a share of 770% of Strength (game data Warmaster.Toughness.MeleeAoeSlow.StrengthScalerPerHit).
    /// UNVERIFIED: the game data names it per hit; the engine gives it once per cast.
    /// </summary>
    public const double ToughnessShare = 0.25;
    public const double ShieldsUpReductionSeconds = 6.0;

    /// <summary>The slow on every enemy hit.</summary>
    public static readonly AuraDef Slow = AuraDef.Effect("Shockwave", SlowEffectId, SlowSeconds);

    /// <summary>Extra Toughness per cast, as a share of 770% of Strength (Greater Shockwave).</summary>
    public double BonusToughnessShare { get; set; }

    /// <summary>Raised for each enemy a cast's hit struck (Aftershock).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public Shockwave(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.StrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        AoeTargetThreshold = Constants.TargetCountThreshold;
        DamageLogId = LogIds.Effect(DamageEffectId);
        VeteranOfWarReductions.Add((typeof(ShieldsUp), ShieldsUpReductionSeconds));
    }

    /// <summary>One Shockwave's hits, the cast's or a free one's (Vanguard's Advance).</summary>
    public void Explode(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        var targets = ctx.Outcomes.SelectTargets(DamageLogId, Enemies(ctx, target), ctx);
        if (targets.Count > AbilityMaxTargets) targets = targets.Take(AbilityMaxTargets).ToList();
        double falloff = targets.Count > Constants.TargetCountThreshold ? Math.Sqrt((double)Constants.TargetCountThreshold / targets.Count) : 1.0;
        for (int i = 0; i < targets.Count; i++)
        {
            var enemy = targets[i];
            DealDamage(ctx, enemy, damageMultiplier: falloff);
            enemy.Auras.Apply(Slow, now);
            Struck?.Invoke(ctx, enemy);
        }
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Explode(ctx, target);
        Helena.GainToughness(ToughnessShare + BonusToughnessShare, ctx);
        ApplyVeteranOfWar(ctx);
    }
}
