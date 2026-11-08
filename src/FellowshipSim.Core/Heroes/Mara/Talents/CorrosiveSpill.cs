using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 206: "Each Combo Point you spend has a chance to cause you to coat the ground in front of you with a
/// Corrosive Spill. Enemies that stand in Corrosive Spill take Agility poison damage periodically."
/// Creeping Death adds damage per Haste. The damage is logged as effect 2310. UNVERIFIED: no combat log has
/// this talent; a sim puts every enemy in the spill and ticks it every <see cref="TickInterval"/> shortened by Haste.
/// </summary>
public partial class CorrosiveSpill : Talent
{
    public const int DamageEffectId = 2310;
    public const string ProcKey = "corrosive_spill";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        var spill = new Spill(mara);
        mara.PoisonDamageIds.Add(spill.DamageLogId);
        hero.OnResourceSpent += (resource, amount, _) =>
        {
            if (resource != Mara.ComboPoints || mara.SimContext is not { } ctx) return;
            for (int i = 0; i < (int)amount; i++)
            {
                if (!ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) continue;
                PeriodicEffect.BeginHasted(ctx, mara, TalentName, TalentDuration, TickInterval, tickOnStart: false, partialFinalTick: false,
                    onTick: (c, _) => spill.DealAoeDamage(c, c.ActiveEnemies), damageLogId: spill.DamageLogId);
            }
        };
    }

    /// <summary>The spill's damage: <see cref="AgilityCoefficient"/> of Agility per tick on every enemy in it.</summary>
    private sealed class Spill : MaraAbility
    {
        public Spill(Mara owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = AgilityCoefficient;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
