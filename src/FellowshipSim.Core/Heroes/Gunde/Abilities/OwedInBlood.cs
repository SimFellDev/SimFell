namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2298 / effect 3245: "Each time Rend deals damage there is a chance a Blood Feather is spawned near the enemy on
/// the ground that you can pick up to gain 1 stack of Owed in Blood for 30 seconds, up to maximum of 150 stacks. Enemies
/// that die with your Rend active on them have a 25% chance to create a Blood Feather. When you use your Owed in Blood
/// ability, all stacks of Owed in Blood you have accumulated from Blood Feathers are applied to the targeted enemy as
/// Rend stacks." Cooldown 3s. Combat logs: the stacks last 90s (patch 0.4.5.0; logs from before 16 September 2026 show
/// 45s); it takes no global cooldown (Gunde guide corpus).
/// Each stack becomes a Rend stack: a Strength of Rend still to deal. Combat logs (Y4wpb2f3gc8LqJDX fights 14 and 25, 41
/// casts): casts of up to 15 stacks add exactly as many Rend stacks; larger ones add 0.6-1.1 per stack (150 -> 127, 94 -> 81,
/// 55 -> 35), 0.87 on average.
/// </summary>
public class OwedInBlood : GundeAbility
{
    public const int AbilityId = 2298;
    public const string AbilityName = "Owed in Blood";
    public const double CooldownSeconds = 3.0;
    public const int BuffEffectId = 3245;
    public const double BuffSeconds = 90.0;
    public const int MaxStacks = 150;

    /// <summary>"Holding on to Rend damage." A stack per Blood Feather picked up.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds, MaxStacks);

    /// <summary>Owed in Blood applied its stacks to an enemy (Bloodcraze).</summary>
    public event Action<SimulationContext, EnemyTarget, int>? Spent;

    /// <summary>
    /// The stacks Owed in Blood had when it was last removed, and when. A replay follows the log's stacks, and the log
    /// removes them in the same moment as the cast that uses them, so the cast may come just after.
    /// </summary>
    private (int Stacks, SimTime At) _removed;

    public OwedInBlood(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Utility)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        owner.Auras.OnRemoved += (aura, now) =>
        {
            if (aura.Def == Buff) _removed = (aura.Stacks, now);
        };
    }

    /// <summary>The stacks the cast uses: those held, or those the same moment removed.</summary>
    private int Stacks(SimTime now)
    {
        int held = Gunde.Auras.GetStacks(Buff, now);
        return held > 0 ? held : _removed.At == now ? _removed.Stacks : 0;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Stacks(ctx.CurrentTime) == 0)
        {
            reason = explain ? $"{Name} needs a stack of Owed in Blood" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        var now = ctx.CurrentTime;
        int stacks = Stacks(now);
        Gunde.Auras.Remove(Buff, now);
        _removed = default;
        if (stacks == 0 || target is not { IsSleeping: false }) return;
        // A Rend stack is a Strength of Rend still to deal (Deep Rend included).
        Gunde.Rend.Apply(ctx, target, stacks * Gunde.PrimaryAttribute(now) / Gunde.Rend.TickRate);
        Spent?.Invoke(ctx, target, stacks);
    }
}
