namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1209: Mara's weapon swing, 30% of Agility (game data coefficient 0.3). Combat logs record every swing as a
/// cast; an action list uses it as soon as the swing timer allows. Combat logs (Fc8v6yWxD14GQMnV fight 19, 13% Haste):
/// swings 1.30s apart, 1.035s during Spirit of Heroism, a 1.5s swing shortened by Haste; no swing in Stealth.
/// </summary>
public class Attack : MaraAbility
{
    public const int AbilityId = 1209;
    public const string AbilityName = "Attack";
    public const double Coefficient = 0.3;
    public const double SwingSeconds = 1.5;

    /// <summary>A swing landed on an enemy (Hemotoxin).</summary>
    public event Action<SimulationContext, EnemyTarget>? Swung;

    public Attack(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(SwingSeconds, scalesWithHaste: true);
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
