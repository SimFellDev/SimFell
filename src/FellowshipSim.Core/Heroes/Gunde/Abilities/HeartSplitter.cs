namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2294: "Jump and strike at your enemy from above, dealing damage to the target. The direct
/// damage dealt by Heart Splitter is added to Rend. Exsanguinate: Heart Splitter exsanguinates the target's wounds,
/// dealing additional damage from the Total Rend Damage active on the target." The cooldown is shortened by Haste.
/// Combat logs (n7zxK8harZT2wjDB fight 9 @ 16.404, Bloodfang#2): Exsanguinate (effect 3239) comes with the hit, after the
/// hit's own Rend is added to the target's Rend. "The Total Rend Damage active" is the
/// Rend still to deal: with the Rend already dealt in it, Exsanguinate comes out above the logs (Y4wpb2f3gc8LqJDX
/// fight 25, 1375 hits), without it much nearer. It neither crits nor takes damage multipliers.
/// </summary>
public partial class HeartSplitter : GundeAbility
{
    public const double RendShareOfDamage = 1.0;
    /// <summary>
    /// Exsanguinate's effect. Its calculation preset is Empty, with ApplyTargetIncomingDamageHealingMultipliers off
    /// (GE_Gunde_HeavyMeleeDotBased_DotBonusDamage, build 26320967), so it takes none of the target's incoming-damage
    /// multipliers. Logs: its hits on an Anomalous Orb are 0.8 to 1.3 times its hits on another enemy of the pulse in 77
    /// of 77 pairs (Ab1yCxVcNBHPz6t8 fight 7).
    /// </summary>
    public const int ExsanguinateEffectId = 3239;
    public const string ExsanguinateName = "Exsanguinate";
    public const double ExsanguinateShare = 0.3;

    /// <summary>Per-enemy crit chance added to its hits (Darkening Hearts, Bleeding Hearts).</summary>
    public List<Func<EnemyTarget, double>> TargetCritChanceBonuses { get; } = new();

    /// <summary>A strike's Exsanguinate dealt its damage to the target; the flag says the strike crit (Oathshatter).</summary>
    public event Action<SimulationContext, EnemyTarget, double, bool>? Exsanguinated;

    /// <summary>Heart Splitter was cast at an enemy (Bleeding Hearts' extra strike).</summary>
    public event Action<SimulationContext, EnemyTarget, RendCast>? Cast;

    private bool _lastHitCrit;

    public HeartSplitter(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        TargetedOffensive = true;
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        RendShare = RendShareOfDamage;
        Hit += (in hit) => _lastHitCrit = hit.IsCrit;
    }

    /// <summary>Bleeding Hearts: "Your Heart Splitter ability has additional Charges".</summary>
    public void SetCharges(int charges) => SetCooldown(AbilityCooldown, charges, scalesWithHaste: true);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var rend = BeginRend(ctx);
        Strike(ctx, target, rend);
        Cast?.Invoke(ctx, target, rend);
    }

    /// <summary>
    /// One strike at <paramref name="target"/>: the hit, its Rend, then Exsanguinate. An <paramref name="extra"/> strike
    /// (Bleeding Hearts) has no share of the cast's damage multiplier (Crimson Strikes): combat logs (Y4wpb2f3gc8LqJDX
    /// fight 25, 6 casts with both strikes crit) put it below the cast's strike.
    /// </summary>
    public void Strike(SimulationContext ctx, EnemyTarget target, RendCast rend, bool extra = false) =>
        RendingPulse(ctx, rend, Single(target), damageMultiplier: extra ? 1.0 / rend.DamageMultiplier : 1.0, extraCritChance: CritChanceAgainst,
            onLanded: (c, enemy, dealt) => { if (dealt > 0.0) Exsanguinate(c, enemy, _lastHitCrit); });

    private double CritChanceAgainst(EnemyTarget target)
    {
        double bonus = 0.0;
        foreach (var b in TargetCritChanceBonuses) bonus += b(target);
        return bonus;
    }

    private void Exsanguinate(SimulationContext ctx, EnemyTarget target, bool crit)
    {
        double damage = ExsanguinateShare * Gunde.Rend.Remaining(target);
        if (damage <= 0.0) return;
        Exsanguinated?.Invoke(ctx, target, damage, crit);
        ctx.ApplyDamage(Owner, target, damage, LogIds.Effect(ExsanguinateEffectId), ExsanguinateName, canCrit: false, 0.0, 1.0,
            abilityGroup: Name);
    }
}
