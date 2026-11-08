using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 585: Hemorrhaging Strike's bleed ticks faster, and each time you deal damage to an enemy with
/// Hemorrhaging Strike's bleed effect, you have a chance to summon a Shadowy Clone to assault that enemy with a Queen's Fang
/// at maximum combo points and full Power. UNVERIFIED: no combat log has it; the clone's Queen's Fang is taken to be logged
/// as Queen's Fang (ability 1201), at once.
/// </summary>
public partial class FromTheShadows : Legendary
{
    /// <summary>The bleed's tick rate bonus that shortens its period to <see cref="TickPeriodMultiplier"/> of it.</summary>
    public const double TickRateBonus = 1.0 / TickPeriodMultiplier - 1.0;
    public const int CloneComboPoints = 6;
    public const double ClonePower = 1.0;
    public const string ProcKey = "from_the_shadows";

    public override void Bind(Hero hero)
    {
        if (hero is not Mara mara || mara.GetAbility<QueensFang>() is not { } queensFang) return;
        mara.Hemorrhage.TickRateBonuses.Add(_ => TickRateBonus);
        // Every hit on an enemy with the bleed rolls, the clone's own Queen's Fang too.
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.Amount <= 0 || hit.Target is not { } target || hit.SimContext is not { } ctx || !mara.Hemorrhage.IsOn(target, ctx)) return;
            if (ctx.Outcomes.RollProc(ProcKey, ActivationChancePerTarget, ctx, target)) queensFang.CloneStrike(ctx, target, CloneComboPoints, ClonePower);
        };
    }
}
