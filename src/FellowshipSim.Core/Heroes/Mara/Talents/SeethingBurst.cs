using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 668: "Each time your Seething Poison deals damage to an enemy, it has a 20% chance to erupt, dealing 150%
/// Agility poison damage to nearby enemies." Creeping Death adds 0.5% per 1% Haste. Damage falls off as sqrt(10/N)
/// beyond 10 targets (ability 1131's targetCountFalloff, effect 3144). UNVERIFIED: no combat log has this talent; the
/// damage is taken to be logged as effect 3144.
/// </summary>
public class SeethingBurst : Talent
{
    public const int TalentId = 668;
    public const string TalentName = "Seething Burst";
    public const int DamageEffectId = 3144;
    public const double Chance = 0.2;
    public const double Coefficient = 1.5;
    public const double TargetThreshold = 10;
    public const string ProcKey = "seething_burst";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        var burst = new Burst(mara);
        mara.PoisonDamageIds.Add(burst.DamageLogId);
        mara.SeethingPoison.Ticked += (ctx, target) =>
        {
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx, target)) burst.DealAoeDamage(ctx, ctx.ActiveEnemies);
        };
    }

    private sealed class Burst : MaraAbility
    {
        public Burst(Mara owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = Coefficient;
            AoeTargetThreshold = TargetThreshold;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
