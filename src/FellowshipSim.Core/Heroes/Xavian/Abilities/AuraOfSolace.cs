namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1917: "Burns all enemies within a large radius for magical damage every 1.5 seconds while active and increases
/// your Parry chance by +8%. Aura of Solace transfers 10% of all damage taken by nearby allies to you. Can be used during
/// Global Cooldown. Costs 10 Mana every 1.5 seconds while active." A cast turns it on, the next turns it off. Cooldown 3s.
/// </summary>
public class AuraOfSolace : XavianAbility
{
    public const int AbilityId = 1917;
    public const string AbilityName = "Aura of Solace";
    public const double CooldownSeconds = 3.0;
    /// <summary>Ability 1915 / effect 2669: 34% of Strength each pulse, period 1.5.</summary>
    public const double Coefficient = 0.34;
    public const int DamageEffectId = 2669;
    /// <summary>
    /// Combat logs (XyGpcHjNaAzgk7mW fight 20, 1,735 hits): a pulse every 1.50s from the cast whatever the Haste.
    /// </summary>
    public const double PulseSeconds = 1.5;
    /// <summary>Game data cost 10 Mana each pulse.</summary>
    public const double ManaPerPulse = 10;
    /// <summary>Game data targetCountFalloff: effect 2669, threshold 8; maxTargets 20.</summary>
    public const double TargetThreshold = 8;
    /// <summary>
    /// A replay deals each pulse at its group of log hits (<see cref="Hero.AddLoggedPulse"/>): the log has no pulse while
    /// no enemy is within the radius, and in some fights the pulses end while effect 2667 stays up (NOTES.md).
    /// </summary>
    public const string PulseProcKey = "aura_of_solace_pulse";

    /// <summary>Effect 2667: up until the next cast.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, 2667);

    private int _aura;

    public AuraOfSolace(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        AoeTargetThreshold = TargetThreshold;
        DamageLogId = LogIds.Effect(DamageEffectId);
    }

    public bool IsActive(SimTime now) => Xavian.Auras.HasAura(Buff, now);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        int aura = ++_aura;
        if (IsActive(now))
        {
            Xavian.Auras.Remove(Buff, now);
            return;
        }
        Xavian.Auras.Apply(Buff, now);
        if (Xavian.PulsesFollowLog) return;

        void Chain(SimulationContext c)
        {
            if (aura != _aura || !IsActive(c.CurrentTime)) return;
            Pulse(c);
            c.Schedule(SimTime.FromSeconds(PulseSeconds), Chain, AbilityName);
        }
        ctx.Schedule(SimTime.FromSeconds(PulseSeconds), Chain, AbilityName);
    }

    /// <summary>One pulse: its Mana and its damage to every enemy within the radius.</summary>
    public void Pulse(SimulationContext ctx)
    {
        Xavian.DrainMana(ManaPerPulse, ctx);
        if (ctx.ActiveEnemies.Count > 0) DealAoeDamage(ctx, ctx.ActiveEnemies);
    }

    public void ResetState() => _aura = 0;
}
