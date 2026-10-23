namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1231 (Spirit ability, 100 Spirit Points): "Instantly Leap to target area and smash the ground, dealing
/// lightning damage to all nearby enemies. You also gain an Enhanced Thunder Call for 20 seconds that causes you to release
/// Raging Currents at enemies over time." Grants Spirit of Heroism (+30% Haste for 20s). Effect 1766 is the Enhanced
/// Thunder Call: +1% Expertise per stack, up to 20.
/// </summary>
public class RagingTempest : TariqAbility
{
    public const int AbilityId = 1231;
    public const string AbilityName = "Raging Tempest";
    /// <summary>Placeholder TempestLeapDmg: 1006% of Strength.</summary>
    public const double Coefficient = 10.06;
    /// <summary>Game data targetCountFalloff: effect 2173, threshold 5.</summary>
    public const double TargetThreshold = 5;
    public const int BuffEffectId = 1766;
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 20;
    public const double ExpertisePerStack = 0.01;
    /// <summary>
    /// Combat logs (YC861NTRgLbmw4Qa fight 31, 11 casts): the leap lands 0.30-0.46s after the cast. The earliest, so a
    /// replay finds the logged hits after it.
    /// </summary>
    public const double LandingSeconds = 0.30;

    /// <summary>
    /// Combat logs (fXyzwGcQkTrLD34R fight 4 @ 263.6): the buff comes with the cast at 1 stack, each Raging Currents hit
    /// adds one, and it lasts 20s from the cast whatever the stacks.
    /// </summary>
    public static readonly AuraDef Buff = AuraDef.Effect("Raging Tempest", BuffEffectId, BuffSeconds, MaxStacks,
        StatModifier.Add(StatType.Expertise, ExpertisePerStack)) with { ReapplyKeepsExpiry = true };

    private readonly RagingCurrents _currents;

    public RagingTempest(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Spirit)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
        IgnoresGcd = true;
        _currents = new RagingCurrents(owner);
    }

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
        var hero = Tariq;
        hero.Resources.Consume(Hero.SpiritPoints, Math.Min(hero.SpiritAbilityCost, hero.ResourceAmount(Hero.SpiritPoints)));
        hero.ActivateSpiritOfHeroism(ctx);
        // FellowshipAnalyzer corpus (45 logs): Thunder Call is removed within 50ms of every Raging Tempest.
        hero.Auras.Remove(ThunderCall.Buff, ctx.CurrentTime);
        hero.Auras.Apply(Buff, ctx.CurrentTime);
        ctx.Schedule(SimTime.FromSeconds(LandingSeconds),
            c => DealAoeDamage(c, WildSwing.AreaAround(c, target), extraCritChance: hero.LightningCritChance), Name);

        // Combat logs (fight 4 @ 263.6): strikes every hasted second from the cast (0.635s apart at 57% haste), until the
        // buff ends.
        PeriodicEffect.BeginHasted(ctx, hero, RagingCurrents.AbilityName, BuffSeconds, RagingCurrents.PeriodSeconds,
            tickOnStart: false, partialFinalTick: false, onTick: (c, _) => _currents.Strike(c));
    }
}
