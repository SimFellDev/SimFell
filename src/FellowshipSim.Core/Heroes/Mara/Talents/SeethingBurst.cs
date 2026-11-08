using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 668: "Each time your Seething Poison deals damage to an enemy, it has a chance to erupt, dealing
/// Agility poison damage to nearby enemies." Creeping Death adds damage per Haste. Damage falls off with the target count
/// beyond a threshold (ability 1131's targetCountFalloff, effect 3144). UNVERIFIED: no combat log has this talent; the
/// damage is taken to be logged as effect 3144.
/// </summary>
public partial class SeethingBurst : Talent
{
    public const int DamageEffectId = 3144;
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
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx, target)) burst.DealAoeDamage(ctx, ctx.ActiveEnemies);
        };
    }

    private sealed class Burst : MaraAbility
    {
        public Burst(Mara owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = AgilityCoefficient;
            AoeTargetThreshold = NumTargetsDamageFalloffThreshold;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
