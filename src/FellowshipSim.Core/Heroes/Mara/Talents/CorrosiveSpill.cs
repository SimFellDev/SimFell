using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 206: "Each Combo Point you spend has a 3% chance to cause you to coat the ground in front of you with a
/// Corrosive Spill for 3 seconds. Enemies that stand in Corrosive Spill take 158% Agility poison damage every 1.5
/// seconds." Creeping Death adds 0.5% per 1% Haste. The damage is logged as effect 2310. UNVERIFIED: no combat log has
/// this talent; a sim puts every enemy in the spill and ticks it every 1.5s shortened by Haste.
/// </summary>
public class CorrosiveSpill : Talent
{
    public const int TalentId = 206;
    public const string TalentName = "Corrosive Spill";
    public const int DamageEffectId = 2310;
    public const double ChancePerComboPoint = 0.03;
    public const double SpillSeconds = 3.0;
    public const double TickSeconds = 1.5;
    public const double Coefficient = 1.58;
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
                if (!ctx.Outcomes.RollProc(ProcKey, ChancePerComboPoint, ctx)) continue;
                PeriodicEffect.BeginHasted(ctx, mara, TalentName, SpillSeconds, TickSeconds, tickOnStart: false, partialFinalTick: false,
                    onTick: (c, _) => spill.DealAoeDamage(c, c.ActiveEnemies));
            }
        };
    }

    /// <summary>The spill's damage: 158% of Agility per tick on every enemy in it.</summary>
    private sealed class Spill : MaraAbility
    {
        public Spill(Mara owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = Coefficient;
            HasCreepingDeath = true;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
