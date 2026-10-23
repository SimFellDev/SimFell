namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2294: "Jump and strike at your enemy from above, dealing 470% of Strength to the target. 100% of the direct
/// damage dealt by Heart Splitter is added to Rend. Exsanguinate: Heart Splitter exsanguinates the target's wounds,
/// dealing an additional 30% of the Total Rend Damage active on the target." Cooldown 12s, shortened by Haste.
/// Combat logs (n7zxK8harZT2wjDB fight 9 @ 16.404, Bloodfang#2): Exsanguinate (effect 3239) comes with the hit, after the
/// hit's own Rend is added: 4492 = 0.3 x 1.1 (Deep Rend) x (621.5 + 1.85 x 7022). It neither crits nor takes damage
/// multipliers.
/// </summary>
public class HeartSplitter : GundeAbility
{
    public const int AbilityId = 2294;
    public const string AbilityName = "Heart Splitter";
    public const double Coefficient = 4.7;
    public const double CooldownSeconds = 12.0;
    public const double RendShareOfDamage = 1.0;
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

    public HeartSplitter(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        RendShare = RendShareOfDamage;
        Hit += (in hit) => _lastHitCrit = hit.IsCrit;
    }

    /// <summary>Bleeding Hearts: "Your Heart Splitter ability has 2 Charges".</summary>
    public void SetCharges(int charges) => SetCooldown(CooldownSeconds, charges, scalesWithHaste: true);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var rend = BeginRend(ctx);
        Strike(ctx, target, rend);
        Cast?.Invoke(ctx, target, rend);
    }

    /// <summary>One strike at <paramref name="target"/>: the hit, its Rend, then Exsanguinate.</summary>
    public void Strike(SimulationContext ctx, EnemyTarget target, RendCast rend) =>
        RendingPulse(ctx, rend, Single(target), extraCritChance: CritChanceAgainst,
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
        ctx.ApplyDamage(Owner, target, damage, LogIds.Effect(ExsanguinateEffectId), ExsanguinateName, canCrit: false, 0.0, 1.0);
    }
}
