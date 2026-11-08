using System.Globalization;
using System.Text.RegularExpressions;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 34 / ability 167: damage has a chance (rank PPM) to conjure an Emerald Spike for Main Stat damage
/// (effect 93), which also triggers the Emerald gem's First Strike.
/// </summary>
public partial class EmeraldJudgement : Aspect
{
    public const int DamageEffectId = 93;
    public const string DamageName = "Emerald Judgement";
    public const double Coefficient = 8.0;
    public const string ProcKey = "emerald_judgement";
    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        var rppm = new RealPpm(ProcKey, ForRank(PPMByRank));
        int logId = LogIds.Effect(DamageEffectId);
        const string name = DamageName;
        const double coefficient = Coefficient;

        void Erupt(SimulationContext ctx, EnemyTarget target)
        {
            // The spike also triggers First Strike, when it lands (combat logs: First Strike refreshes with the spike's hit).
            var firstStrike = hero.Gems.OfType<Emerald>().FirstOrDefault()?.FirstStrike;
            ctx.ApplyDamage(hero, target, logId, name, canCrit: true, c => new SimulationContext.HitNumbers(
                coefficient * hero.PrimaryAttribute(c.CurrentTime) * (1.0 + hero.Expertise(c.CurrentTime))
                    * hero.DamageScale(c.CurrentTime) * hero.DamageMultiplierAgainst(target, c.CurrentTime),
                hero.CritChanceAgainst(target, c.CurrentTime),
                hero.CritMultiplier(c.CurrentTime)),
                onLanded: firstStrike == null ? null : (c, dealt) => { if (dealt > 0.0) hero.Auras.Apply(firstStrike, c.CurrentTime); });
        }

        // Speed: the handler captures nothing per hit; the spike's closures are made only when it erupts.
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.Target == null || dmg.Target.IsSleeping || dmg.SimContext == null || dmg.AbilityId == logId) return;
            // The spike erupts under the enemy whose hit triggered it.
            if (rppm.Roll(hero, dmg.SimContext, dmg.Target)) Erupt(dmg.SimContext, dmg.Target);
        };
    }
}
