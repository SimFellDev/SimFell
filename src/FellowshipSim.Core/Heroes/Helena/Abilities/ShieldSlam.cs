namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 977: "Deals 601.496% of Strength physical damage to your target and 75.8% of Strength physical damage to other
/// enemies near your target. Shield Slam gains Cooldown Acceleration equal to your Haste. Shield Slam generates 15%
/// Toughness." Lingering Concussion (effect 2478) on the main target, "3% reduced damage to you for 12 seconds", up to 5
/// stacks. "VETERAN OF WAR: Shield Slam reduces the cooldown of your Shockwave ability by 3.0 seconds." 2 charges, 7.5s
/// each; the other enemies' hits lose damage as sqrt(8/N) beyond 8 enemies (effect 1264). Every hit is logged as 977.
/// </summary>
public class ShieldSlam : HelenaAbility
{
    public const int AbilityId = 977;
    public const string AbilityName = "Shield Slam";
    public const double Coefficient = 6.01496;
    public const double OtherEnemiesCoefficient = 0.758;
    public const double CooldownSeconds = 7.5;
    public const int Charges = 2;
    public const double TargetThreshold = 8;
    /// <summary>
    /// Toughness per cast, as a share of 770% of Strength (game data Warmaster.Toughness.MeleeCleave.StrengthScalerPerHit).
    /// UNVERIFIED: the game data names it per hit; the engine gives it once per cast.
    /// </summary>
    public const double ToughnessShare = 0.15;
    public const double ShockwaveReductionSeconds = 3.0;
    public const int LingeringConcussionEffectId = 2478;
    public const double LingeringConcussionSeconds = 12.0;
    public const int LingeringConcussionMaxStacks = 5;

    /// <summary>Lingering Concussion on the main target: it deals less damage to Helena. No damage of hers changes.</summary>
    public static readonly AuraDef LingeringConcussion = AuraDef.Effect("Lingering Concussion", LingeringConcussionEffectId,
        LingeringConcussionSeconds, LingeringConcussionMaxStacks);

    /// <summary>Share of Armor (<see cref="Helena.Armor"/>) every hit adds to its damage (Gleaming Shield).</summary>
    public double ArmorDamageShare { get; set; }

    /// <summary>Raised as the cast resolves, after its hits (Hold the Line's Empowered Shield Slam uses it up).</summary>
    public event Action<SimulationContext>? Slammed;

    public ShieldSlam(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
        AoeTargetThreshold = TargetThreshold;
        VeteranOfWarReductions.Add((typeof(Shockwave), ShockwaveReductionSeconds));
    }

    /// <summary>The main target's damage, with the flat Armor part (Gleaming Shield).</summary>
    public override double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0)
    {
        double damage = base.BaseDamage(ctx, target, damageMultiplier);
        if (ArmorDamageShare <= 0) return damage;
        double strengthPart = BaseCoefficient * Owner.PrimaryAttribute(ctx.CurrentTime);
        return damage * (strengthPart + ArmorDamageShare * Helena.Armor(ctx.CurrentTime)) / strengthPart;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        var targets = ctx.Outcomes.SelectTargets(DamageLogId, Enemies(ctx, target), ctx);
        double falloff = targets.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / targets.Count) : 1.0;
        // Another enemy's hit is its 75.8% with the same flat Armor part as the main target's.
        double strength = Owner.PrimaryAttribute(now);
        double armorPart = ArmorDamageShare * Helena.Armor(now);
        double otherShare = (OtherEnemiesCoefficient * strength + armorPart) / (Coefficient * strength + armorPart);
        for (int i = 0; i < targets.Count; i++)
        {
            var enemy = targets[i];
            DealDamage(ctx, enemy, damageMultiplier: enemy == target ? 1.0 : otherShare * falloff);
        }
        if (target is { IsSleeping: false }) target.Auras.Apply(LingeringConcussion, now);
        Helena.GainToughness(ToughnessShare, ctx);
        ApplyVeteranOfWar(ctx);
        Slammed?.Invoke(ctx);
    }
}
