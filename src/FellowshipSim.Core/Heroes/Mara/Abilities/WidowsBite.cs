namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1207: a right hand strike and a left hand strike on a single target. Widow's Bite gains Cooldown Acceleration
/// equal to your Haste, generates Combo Points with each strike (more on a Critical Strike) and generates Energy. Used in
/// Stealth it applies Seething Poison (<see cref="SeethingPoison"/>); both strikes are logged as 1207.
/// </summary>
public partial class WidowsBite : MaraAbility
{
    public const double RightHandCoefficient = 1.452;
    public const double LeftHandCoefficient = 1.08;

    /// <summary>Widow's Bite hit an enemy (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public override double NetEnergyCost => EnergyCost - Constants.EnergyGain;

    public WidowsBite(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = RightHandCoefficient;
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
        BreaksStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx) GenerateComboPoints(ctx, hit.IsCrit ? Constants.ComboPointGainAtCrit : Constants.ComboPointGain, hit.Target);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        bool stealthed = Mara.IsStealthed(ctx.CurrentTime);
        TriggerCooldown(ctx);
        Pay(ctx);
        Mara.GainEnergy(Constants.EnergyGain, ctx);
        DealDamage(ctx, target);
        DealDamage(ctx, target, damageMultiplier: LeftHandCoefficient / RightHandCoefficient);
        Struck?.Invoke(ctx, target);
        if (stealthed) Mara.SeethingPoison.ApplyTo(ctx, target);
    }
}
