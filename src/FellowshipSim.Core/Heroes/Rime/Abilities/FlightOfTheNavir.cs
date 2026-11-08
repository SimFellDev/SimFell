using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1009 / effect 2446: Frost Swallows (<see cref="SwallowCount"/>) circle Rime; Cold Snap and Freezing Torrent command them to
/// attack (<see cref="CommandSwallows"/>, <see cref="CommandSwallow"/>).
/// </summary>
public partial class FlightOfTheNavir : RimeAbility
{
    public const int BuffEffectId = 2446;
    public const int SwallowCount = 5;
    /// <summary>Combat logs report the circling swallows' hits under effect 1365.</summary>
    public const int SwallowDamageEffectId = 1365;

    public static readonly AuraDef Buff = AuraDef.Effect("Flight of the Navir", BuffEffectId, Duration);

    /// <summary>The circling swallows: Frost Swallows logged under effect 1365 that come back when they hit.</summary>
    public CirclingSwallows Swallows { get; }

    public FlightOfTheNavir(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = DamageIntellectCoefficient;
        SetCooldown(AbilityCooldown);
        Swallows = new CirclingSwallows(owner);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Rime;
        hero.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }

    /// <summary>Cold Snap: every swallow circling now attacks <paramref name="target"/>.</summary>
    public void CommandSwallows(SimulationContext ctx, EnemyTarget target)
    {
        if (!Rime.Auras.HasAura(Buff, ctx.CurrentTime) || target == null) return;
        // Every swallow circling now; one that lands at once must not be sent again by the same command.
        int available = Swallows.Available;
        for (int i = 0; i < available; i++) Swallows.Send(ctx, target);
    }

    /// <summary>Freezing Torrent: one circling swallow attacks an enemy of its choosing.</summary>
    public void CommandSwallow(SimulationContext ctx)
    {
        if (!Rime.Auras.HasAura(Buff, ctx.CurrentTime) || Swallows.Available == 0) return;
        var enemy = ctx.Outcomes.ChooseTarget(Swallows.DamageLogId, ctx.ActiveEnemies, ctx);
        if (enemy != null) Swallows.Send(ctx, enemy);
    }

    public void ResetState() => Swallows.ResetState();

    /// <summary>Flight of the Navir's Frost Swallows: logged under effect 1365, and only <see cref="SwallowCount"/> of them.</summary>
    public sealed class CirclingSwallows : FrostSwallows
    {
        private int _inFlight;

        public CirclingSwallows(Rime owner) : base(owner, LogIds.Effect(SwallowDamageEffectId), DamageIntellectCoefficient) { }

        /// <summary>
        /// The circling swallows not out attacking: one that is sent comes back when it hits (combat logs: a Cold Snap cast
        /// while a Freezing Torrent swallow is still flying sends one fewer, yMAZVJL9hTpFjqbC fight 7 @ 51.956).
        /// </summary>
        public int Available => Math.Max(0, SwallowCount - _inFlight);

        /// <summary>Sends one circling swallow at <paramref name="target"/>, if one is available.</summary>
        public bool Send(SimulationContext ctx, EnemyTarget target)
        {
            if (Available == 0) return false;
            _inFlight++;
            DealDamage(ctx, target, onLanded: (_, _) => _inFlight = Math.Max(0, _inFlight - 1));
            return true;
        }

        public void ResetState() => _inFlight = 0;
    }
}
