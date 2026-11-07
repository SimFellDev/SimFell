namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1270 (Spirit ability): a cast, then a channel in which a jet of fire deals Intellect fire damage on a period to
/// all enemies in a cone in front of the hero. Each hit extends the duration of the hero's active damage over time effects
/// on the targets and applies a stacking damage over time to them (<see cref="IncinerateDot"/>). Grants Spirit of
/// Heroism. Damage falls off with the target count (game data targetCountFalloff, effect 1830).
/// </summary>
public class Incinerate : ArdeosAbility
{
    public const int AbilityId = 1270;
    public const string AbilityName = "Incinerate";
    public const double Coefficient = 3.2015;
    public const double CastTimeSeconds = 1.5;
    public const double ChannelSeconds = 2.5;
    public const double TickPeriodSeconds = 0.5;
    public const double TargetThreshold = 8;
    public const double ExtensionSeconds = 1.5;

    /// <summary>Game data globalCooldown, hasted (shorter than Fire Ball's).</summary>
    public const double GlobalCooldown = 1.0;

    public IncinerateDot Dot { get; }

    public Incinerate(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
        GlobalCooldownSeconds = GlobalCooldown;
        // UNVERIFIED: castable while the global cooldown runs (in-game play); the game data record has no flag for it.
        UsableDuringGcd = true;
        SetChannel(ChannelSeconds, TickPeriodSeconds);
        // Combat logs: the first hits come one server frame after the channel starts (yMAZVJL9hTpFjqbC fight 7).
        ChannelTicksOnStart = true;
        ChannelPartialFinalTick = true;
        AoeTargetThreshold = TargetThreshold;
        Dot = new IncinerateDot(owner);
        Hit += (in DamageContext hit) =>
        {
            if (hit.Target == null || hit.SimContext is not { } ctx) return;
            var dots = owner.Dots;
            // Combat logs (yMAZVJL9hTpFjqbC fight 7, Fire Ball on 19.4 @ 42.08-47.45s): the hits leave the effect its
            // full duration after the last one; an effect never has more than its full duration left.
            foreach (var dot in dots.On(hit.Target).Where(d => d.Effect != Dot).ToList())
            {
                dots.Extend(ctx, dot, ExtensionSeconds, maxRemainingSeconds: dot.FullDurationSeconds);
            }
            dots.Apply(ctx, Dot, hit.Target);
        };
    }

    /// <summary>Ardeos's Spirit ability: it needs and spends the Spirit ability's cost in Spirit Points.</summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        double points = Owner.ResourceAmount(Hero.SpiritPoints);
        if (points < Owner.SpiritAbilityCost)
        {
            reason = explain ? $"{Name} needs {Owner.SpiritAbilityCost:0} Spirit Points (have {points:0.#})" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Owner.Resources.Consume(Hero.SpiritPoints, Math.Min(Owner.SpiritAbilityCost, Owner.ResourceAmount(Hero.SpiritPoints)));
        Owner.ActivateSpiritOfHeroism(ctx);
    }

    public override void OnChannelTick(SimulationContext ctx, EnemyTarget target, double fraction)
    {
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 && target != null ? [target] : ctx.ActiveEnemies;
        DealAoeDamage(ctx, enemies, damageMultiplier: fraction);
    }
}
