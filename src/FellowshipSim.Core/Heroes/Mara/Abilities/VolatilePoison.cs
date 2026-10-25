namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1731, from Skittering Blades used in Stealth: "Applies Volatile Poison to all enemies hit by your Skittering
/// Blades, dealing 31.2% of Agility poison damage over 6 seconds. Upon expiration, Volatile Poison erupts dealing 124.8%
/// of Agility poison damage to the target" (Brooding Shadows' text); the eruption is logged as effect 1730. Creeping
/// Death adds 0.5% per 1% Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 508 poisons): each poison lasts between 6 and 8 seconds (effect 1731
/// DurationMagnitude 6 to 8) and ticks 6 times raised by Haste, rounded (7 at 13% Haste, 9 during Spirit of
/// Heroism), evenly spread, the last as it runs out; the eruption follows 0.2s later. The engine takes the shortest
/// duration, so no tick comes after the log's.
/// </summary>
public class VolatilePoison : MaraAbility
{
    public const int EffectId = 1731;
    public const int EruptionEffectId = 1730;
    public const string EffectName = "Volatile Poison";
    /// <summary>Combat logs (fight 19, 3,687 ticks): each tick deals the 31.2% the text gives for the whole poison.</summary>
    public const double TickCoefficient = 0.312;
    public const int BaseTicks = 6;
    public const double EruptionCoefficient = 1.248;
    public const double DurationSeconds = 6.0;
    public const double EruptionDelaySeconds = 0.2;

    public static readonly AuraDef Debuff = AuraDef.Effect(EffectName, EffectId);

    private readonly Eruption _eruption;
    private readonly Dictionary<EnemyTarget, int> _version = new();
    private SimulationContext? _fight;

    public VolatilePoison(Mara owner) : base(owner, LogIds.Effect(EffectId), EffectName)
    {
        BaseCoefficient = TickCoefficient;
        HasCreepingDeath = true;
        _eruption = new Eruption(owner);
    }

    /// <summary>Puts the poison on <paramref name="target"/>; on an enemy that has it, it starts again.</summary>
    public void ApplyTo(SimulationContext ctx, EnemyTarget target)
    {
        if (_fight != ctx)
        {
            _version.Clear();
            _fight = ctx;
        }
        int version = _version.GetValueOrDefault(target) + 1;
        _version[target] = version;
        target.Auras.Apply(Debuff, ctx.CurrentTime);
        int ticks = Math.Max(1, (int)Math.Round(BaseTicks * (1.0 + Owner.GetHaste(ctx.CurrentTime))));
        double spacing = DurationSeconds / ticks;
        for (int k = 1; k <= ticks; k++)
        {
            bool last = k == ticks;
            ctx.Schedule(SimTime.FromSeconds(spacing * k), c =>
            {
                if (_version.GetValueOrDefault(target) != version || target.IsSleeping) return;
                DealDamage(c, target);
                if (!last) return;
                _version.Remove(target);
                target.Auras.Remove(Debuff, c.CurrentTime);
                c.Schedule(SimTime.FromSeconds(EruptionDelaySeconds), cc =>
                {
                    if (!target.IsSleeping) _eruption.DealDamage(cc, target);
                }, "Volatile Poison erupts");
            }, Name);
        }
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }

    /// <summary>The eruption: 124.8% of Agility on the poisoned enemy, logged as effect 1730.</summary>
    private sealed class Eruption : MaraAbility
    {
        public Eruption(Mara owner) : base(owner, LogIds.Effect(EruptionEffectId), EffectName)
        {
            BaseCoefficient = EruptionCoefficient;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
