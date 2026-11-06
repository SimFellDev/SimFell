using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 667: "Your Backstab has a chance to apply Caustic Poison outside of Brooding Shadows if the target has
/// Hemorrhaging Strike's Bleed active on them." UNVERIFIED: no combat log has this talent.
/// </summary>
public class CausticWounds : Talent
{
    public const int TalentId = 667;
    public const string TalentName = "Caustic Wounds";
    public const double Chance = 0.3;
    public const string ProcKey = "caustic_wounds";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        var backstab = mara.GetAbility<Backstab>();
        if (backstab == null) return;
        backstab.Struck += (ctx, target) =>
        {
            if (mara.Hemorrhage.IsOn(target, ctx) && ctx.Outcomes.RollProc(ProcKey, Chance, ctx, target)) mara.CausticPoison.Strike(ctx, target);
        };
    }
}
