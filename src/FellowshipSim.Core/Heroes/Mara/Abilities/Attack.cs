namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1209: Mara's weapon swing (game data coefficient <see cref="Coefficient"/>). Combat logs record every swing as a
/// cast; an action list uses it as soon as the swing timer allows. Combat logs (Fc8v6yWxD14GQMnV fight 19):
/// the swing of <see cref="SwingTimer"/> is shortened by Haste, and more during Spirit of Heroism; no swing in Stealth.
/// </summary>
public partial class Attack : MaraAbility
{
    public const double Coefficient = 0.3;

    /// <summary>A swing landed on an enemy (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget>? Swung;

    public Attack(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(SwingTimer, scalesWithHaste: true);
        IgnoresGcd = true;
        UnusableInStealth = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is not { IsSleeping: false }) return;
        DealDamage(ctx, target);
        Swung?.Invoke(ctx, target);
    }
}
