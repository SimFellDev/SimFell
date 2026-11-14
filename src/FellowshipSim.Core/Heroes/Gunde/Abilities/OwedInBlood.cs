namespace FellowshipSim.Core.Heroes.Gunde.Abilities;

/// <summary>
/// Ability 2298 / effect 3245: "Each time Rend deals damage there is a chance a Blood Feather is spawned near the enemy on
/// the ground that you can pick up to gain a stack of Owed in Blood, up to a maximum number of stacks
/// (<see cref="Constants.MaxStacks"/>). Enemies
/// that die with your Rend active on them have a chance to create a Blood Feather. When you use your Owed in Blood
/// ability, all stacks of Owed in Blood you have accumulated from Blood Feathers are applied to the targeted enemy as
/// Rend stacks." The stack duration is <see cref="BuffSeconds"/> (combat logs, patch 0.4.5.0; logs from before
/// 16 September 2026 show a shorter one); the cast takes no global cooldown (Gunde guide corpus).
/// Blueprints BP_Gunde_DotTransfer_Orb and GA_Gunde_DotTransfer (build 26320967): each stack picked up saves a Strength
/// of Rend, with the Strength at pickup, up to <see cref="Constants.MaxStacks"/> Strengths. The cast adds the saved damage to the
/// target's Rend as it stands: no share and no share bonus, and Deep Rend's ticks deal more of it.
/// </summary>
public partial class OwedInBlood : GundeAbility
{
    public const int BuffEffectId = 3245;
    public const double BuffSeconds = 90.0;

    /// <summary>"Holding on to Rend damage." A stack per Blood Feather picked up.</summary>
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds, Constants.MaxStacks);

    /// <summary>Owed in Blood applied its stacks to an enemy (Bloodcraze).</summary>
    public event Action<SimulationContext, EnemyTarget, int>? Spent;

    /// <summary>
    /// The stacks Owed in Blood had when it was last removed, and when. A replay follows the log's stacks, and the log
    /// removes them in the same moment as the cast that uses them, so the cast may come just after.
    /// </summary>
    private (int Stacks, double Saved, SimTime At) _removed;

    /// <summary>The Rend damage the stacks held save (the game's <c>SavedDotDamage</c>).</summary>
    private double _saved;

    public OwedInBlood(Gunde owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
        owner.Auras.OnGained += (aura, before, now) =>
        {
            if (aura.Def != Buff) return;
            double strength = owner.PrimaryAttribute(now);
            _saved = SavedAfterPickup(_saved, Math.Max(0, aura.Stacks - before), strength);
        };
        owner.Auras.OnRemoved += (aura, now) =>
        {
            if (aura.Def != Buff) return;
            _removed = (aura.Stacks, _saved, now);
            _saved = 0.0;
        };
    }

    /// <summary>The saved damage after a pickup of <paramref name="stacks"/> stacks at <paramref name="strength"/>, capped at <see cref="Constants.MaxStacks"/> Strengths.</summary>
    public static double SavedAfterPickup(double saved, int stacks, double strength) =>
        Math.Min(Constants.MaxStacks * strength, saved + stacks * strength);

    /// <summary>The saved damage the cast uses: that of the stacks held, or of those the same moment removed.</summary>
    private double Saved(SimTime now) =>
        Gunde.Auras.GetStacks(Buff, now) > 0 ? _saved : _removed.At == now ? _removed.Saved : 0.0;

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
        double saved = Saved(now);
        Gunde.Auras.Remove(Buff, now);
        _removed = default;
        if (stacks == 0 || target is not { IsSleeping: false }) return;
        Gunde.Rend.Apply(ctx, target, saved);
        Spent?.Invoke(ctx, target, stacks);
    }
}
