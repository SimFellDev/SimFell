using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearAspect;

/// <summary>
/// Weapon abilities and cooldown recovery. Written out and skipped: the user will check in game.
/// </summary>
public class AspectComplexChronoshiftRecovery
{
    /// <summary>
    /// Player: Chronoshift's own cooldown is not sped up by its channel's 800% cooldown recovery ("Asha's Arcane Embrace": the recovery is for your
    /// other abilities; a weapon ability's cooldown runs at normal speed, like all weapon abilities': only cooldown reductions and its own effects
    /// shorten it). Cast and channel last 4 seconds, so 176 of its 180 seconds are left right after.
    /// Dev: a bare ranged hero (Rime) with Chronoshift: the cooldown left after the cast against 180 - 4.
    /// The engine gives 156 (it lets the channel's recovery speed Chronoshift's own cooldown, <c>OwnRecoveryAuraLogIds</c>: 3 seconds at 8x, 20 seconds gained).
    /// TODO: the user is 99% sure that is a sim mistake; check in game whether Chronoshift's cooldown is sped up during its channel. If it is not,
    /// this test shows an engine bug (and Visions of Grandeur's and Brave Machinations' reductions then apply to a 176-second cooldown).
    /// </summary>
    [Fact(Skip = "Unsettled: the engine speeds Chronoshift's own cooldown with its channel's cooldown recovery (176s left expected, 156s in the engine); the user will check in game")]
    public void Chronoshifts_Own_Cooldown_Is_Not_Sped_Up_By_Its_Channel()
    {
        var rime = GearFights.Heroes[0];
        var f = rime.Fight(AspectFights.With(0, weapon: AspectFights.Chronoshift));
        f.Cast(f.AspectWeapon());
        Assert.Equal(180.0 - f.Now.TotalSeconds, f.AspectWeaponCooldownLeft(), 0.05);
    }
}
