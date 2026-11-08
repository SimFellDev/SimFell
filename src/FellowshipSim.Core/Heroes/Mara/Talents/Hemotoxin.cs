using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 205: "Your Backstab, Widow's Bite, and Auto Attacks have a chance to apply a stack of Hemotoxin to the
/// target. causing it to take Agility poison damage periodically. Using Hemorrhaging Strike on a
/// target afflicted with Hemotoxin consumes a stack, instantly dealing a share of Hemorrhaging Strike's bleed damage to the
/// target and a share of Hemorrhaging Strike's bleed damage to other nearby targets as poison damage. The damage is affected
/// by the amount of combo points spent on the Hemorrhaging Strike that triggers Hemotoxin. Hemotoxin's area damage is
/// reduced beyond a target count." Effect 2337 is the poison; the burst is logged as effect 2338.
/// UNVERIFIED: no combat log has this talent; "bleed damage" is taken as the bleed's damage over the duration the
/// Combo Points give it.
/// </summary>
public partial class Hemotoxin : Talent
{
    public const int PoisonEffectId = 2337;
    public const int BurstEffectId = 2338;
    public const int MaxStacks = 3;
    public const string ProcKey = "hemotoxin";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        var poison = new Poison(mara);
        var burst = new Burst(mara);
        mara.PoisonDamageIds.UnionWith([poison.DamageLogId, burst.DamageLogId]);
        mara.Register(poison.Debuff);

        void Roll(SimulationContext ctx, EnemyTarget target)
        {
            if (ctx.Outcomes.RollProc(ProcKey, ExplosivePoisonChance, ctx, target)) poison.AddStack(ctx, target);
        }
        foreach (var backstab in mara.Abilities.OfType<Backstab>()) backstab.Struck += Roll;
        if (mara.GetAbility<WidowsBite>() is { } widowsBite) widowsBite.Struck += Roll;
        if (mara.GetAbility<Attack>() is { } attack) attack.Swung += Roll;

        if (mara.GetAbility<HemorrhagingStrike>() is not { } strike) return;
        strike.Struck += (ctx, target, points) =>
        {
            if (!poison.ConsumeStack(ctx, target)) return;
            var hemorrhage = mara.Hemorrhage;
            double bleed = hemorrhage.TickDamage(ctx, target) * hemorrhage.TicksIn(HemorrhagingStrike.BleedSeconds(points), ctx.CurrentTime);
            burst.Strike(ctx, target, bleed * BleedToPoisonDamageMultiplier);
            var nearby = ctx.ActiveEnemiesExcept(target);
            if (nearby.Count == 0) return;
            double falloff = nearby.Count > TargetScalingThreshold ? Math.Sqrt((double)TargetScalingThreshold / nearby.Count) : 1.0;
            foreach (var enemy in nearby) burst.Strike(ctx, enemy, bleed * AreaDamageMultiplier * falloff);
        };
    }

    /// <summary>The poison: <see cref="AgilityCoefficient"/> of Agility every <see cref="TickInterval"/> for <see cref="Duration"/> per stack, up to <see cref="MaxStacks"/>.</summary>
    private sealed class Poison : MaraDot
    {
        public Poison(Mara owner) : base(owner, PoisonEffectId, TalentName, AgilityCoefficient, TickInterval, Duration) { }

        public void AddStack(SimulationContext ctx, EnemyTarget target) => ApplyStack(ctx, target, MaxStacks);
    }

    /// <summary>The burst: a share of the bleed's damage as poison damage, logged as effect 2338.</summary>
    private sealed class Burst : MaraAbility
    {
        public Burst(Mara owner) : base(owner, LogIds.Effect(BurstEffectId), TalentName) { }

        public void Strike(SimulationContext ctx, EnemyTarget target, double damage) =>
            ctx.ApplyDamage(Owner, target, damage, DamageLogId, Name, canCrit: true, Owner.CritChanceAgainst(target, ctx.CurrentTime),
                CritMultiplierAt(ctx.CurrentTime));

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
