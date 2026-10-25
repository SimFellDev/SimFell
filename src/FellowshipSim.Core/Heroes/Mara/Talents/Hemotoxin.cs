using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 205: "Your Backstab, Widow's Bite, and Auto Attacks have a 12% chance to apply 1 stack of Hemotoxin to the
/// target. causing it to take 20% Agility poison damage every 1.5 seconds over 9 seconds. Using Hemorrhaging Strike on a
/// target afflicted with Hemotoxin consumes 1 stack, instantly dealing 75% of Hemorrhaging Strike's bleed damage to the
/// target and 50% of Hemorrhaging Strike's bleed damage to other nearby targets as poison damage. The damage is affected
/// by the amount of combo points spent on the Hemorrhaging Strike that triggers Hemotoxin. Hemotoxin's area damage is
/// reduced beyond 1 target." Effect 2337 (3 stacks) is the poison; the burst is logged as effect 2338.
/// UNVERIFIED: no combat log has this talent; "bleed damage" is taken as the bleed's damage over the duration the
/// Combo Points give it.
/// </summary>
public class Hemotoxin : Talent
{
    public const int TalentId = 205;
    public const string TalentName = "Hemotoxin";
    public const int PoisonEffectId = 2337;
    public const int BurstEffectId = 2338;
    public const double Chance = 0.12;
    public const double PoisonCoefficient = 0.2;
    public const double PoisonTickSeconds = 1.5;
    public const double PoisonSeconds = 9.0;
    public const int MaxStacks = 3;
    public const double TargetShare = 0.75;
    public const double NearbyShare = 0.50;
    public const double NearbyTargetThreshold = 1;
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
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx, target)) poison.AddStack(ctx, target);
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
            burst.Strike(ctx, target, bleed * TargetShare);
            var nearby = ctx.ActiveEnemiesExcept(target);
            if (nearby.Count == 0) return;
            double falloff = nearby.Count > NearbyTargetThreshold ? Math.Sqrt(NearbyTargetThreshold / nearby.Count) : 1.0;
            foreach (var enemy in nearby) burst.Strike(ctx, enemy, bleed * NearbyShare * falloff);
        };
    }

    /// <summary>The poison: 20% of Agility every 1.5s for 9s per stack, up to 3.</summary>
    private sealed class Poison : MaraDot
    {
        public Poison(Mara owner) : base(owner, PoisonEffectId, TalentName, PoisonCoefficient, PoisonTickSeconds, PoisonSeconds) { }

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
