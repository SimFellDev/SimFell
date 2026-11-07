namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 978: Helena's weapon swing, 40% of Strength (game data coefficient 0.4). Combat logs record every swing as a
/// cast; an action list uses it as soon as the swing timer allows. UNVERIFIED: the swing is taken to be 1.5s shortened by
/// Haste, as Gunde's.
/// </summary>
public class Attack : HelenaAbility
{
    public const int AbilityId = 978;
    public const string AbilityName = "Attack";
    public const double Coefficient = 0.4;
    public const double SwingSeconds = 1.5;

    /// <summary>
    /// Combat logs (19 Helena fights of patch 0.5.0.0): 1,024 of 11,385 swings are a miss or a dodge (log hit types 0 and 7),
    /// a hit of 0 damage. UNVERIFIED estimate: the share of both, as one chance.
    /// </summary>
    public const double AvoidChance = 0.09;
    public const string AvoidedProcKey = "helena_attack_avoided";

    public Attack(Helena owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(SwingSeconds, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target is not { IsSleeping: false }) return;
        // A swing the enemy avoids is a hit of 0 damage.
        bool avoided = ctx.Outcomes.RollProc(AvoidedProcKey, AvoidChance, ctx, target);
        DealDamage(ctx, target, avoided ? 0.0 : 1.0, canCrit: !avoided);
    }
}
