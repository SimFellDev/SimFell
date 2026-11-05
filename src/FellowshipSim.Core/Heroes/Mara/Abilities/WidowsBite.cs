namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1207: "Plunges both daggers into single target, dealing 145.2% of Agility with the right hand and 108% of
/// Agility with the left hand. Widow's Bite gains Cooldown Acceleration equal to your Haste. Generates 2 Combo Points
/// with each strike. Critical Strikes generate 3 Combo Points. Generates 30 Energy." 3 charges, 9s. Used in Stealth it
/// applies Seething Poison (<see cref="SeethingPoison"/>); both strikes are logged as 1207.
/// </summary>
public class WidowsBite : MaraAbility
{
    public const int AbilityId = 1207;
    public const string AbilityName = "Widow's Bite";
    public const double RightHandCoefficient = 1.452;
    public const double LeftHandCoefficient = 1.08;
    public const double CooldownSeconds = 9.0;
    public const int Charges = 3;
    public const double EnergyGained = 30;
    public const double ComboPointsPerStrike = 2;
    public const double ComboPointsOnCrit = 3;

    /// <summary>Widow's Bite hit an enemy (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget>? Struck;

    public override double NetEnergyCost => EnergyCost - EnergyGained;

    public WidowsBite(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = RightHandCoefficient;
        SetCooldown(CooldownSeconds, Charges, scalesWithHaste: true);
        BreaksStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx) GenerateComboPoints(ctx, hit.IsCrit ? ComboPointsOnCrit : ComboPointsPerStrike, hit.Target);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        bool stealthed = Mara.IsStealthed(ctx.CurrentTime);
        TriggerCooldown(ctx);
        Pay(ctx);
        Mara.GainEnergy(EnergyGained, ctx);
        DealDamage(ctx, target);
        DealDamage(ctx, target, damageMultiplier: LeftHandCoefficient / RightHandCoefficient);
        Struck?.Invoke(ctx, target);
        if (stealthed) Mara.SeethingPoison.ApplyTo(ctx, target);
    }
}
