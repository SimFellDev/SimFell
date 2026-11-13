using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 585: Hemorrhaging Strike's bleed ticks faster, and each time you deal damage to an enemy with
/// Hemorrhaging Strike's bleed effect, you have a chance to summon a Shadowy Clone to assault that enemy with a Queen's Fang
/// at maximum combo points and full Power. Game data (build 26320967), ability 1589
/// (`GA_Mara_Passive_Talent_AoeAttackSpender_CloneAttack`): each application of Mara's bleed to an enemy, a refresh and a copy of
/// Gushing Blood's re-application too, multiplies its tick period scaler by <see cref="Constants.TickPeriodMultiplier"/> (offsets
/// 15 to 376, a `WaitGameplayEffectAppliedToTarget` on the bleed's asset tag with no ignore tag), before the applying ability's
/// own writes. Combat logs (fjrCax7VzGmMZw6h fight 26, Mara player 23, the one Mara player with it): ticks 0.722 to 0.739 of
/// 3s / Haste apart after a plain refresh, 0.87 / 1.2 with Bloodrush. The clone's Queen's Fang is logged as Queen's Fang
/// (ability 1201).
/// </summary>
public partial class FromTheShadows : Legendary
{
    public const int CloneComboPoints = 6;
    public const double ClonePower = 1.0;
    public const string ProcKey = "from_the_shadows";

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara || mara.GetAbility<QueensFang>() is not { } queensFang) return;
        mara.Hemorrhage.ApplicationPeriodMultipliers.Add(Constants.TickPeriodMultiplier);
        // Every hit on an enemy with the bleed rolls, the clone's own Queen's Fang too.
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.Amount <= 0 || hit.Target is not { } target || hit.SimContext is not { } ctx || !mara.Hemorrhage.IsOn(target, ctx)) return;
            if (ctx.Outcomes.RollProc(ProcKey, Constants.ActivationChancePerTarget, ctx, target)) queensFang.CloneStrike(ctx, target, CloneComboPoints, ClonePower);
        };
    }
}
