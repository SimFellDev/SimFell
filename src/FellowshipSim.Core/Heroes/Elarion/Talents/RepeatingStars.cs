using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 124: "Your Multishot reduces the cooldown of your Starfall Volley by 1.5 seconds. Empowered Multishot reduces the
/// cooldown by 3.0 seconds."
/// </summary>
public class RepeatingStars : Talent
{
    public const int TalentId = 124;
    public const string TalentName = "Repeating Stars";
    public const double ReductionSeconds = 1.5;
    public const double EmpoweredReductionSeconds = 3.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<Multishot>() is not { } multishot) return;
        multishot.Cast += (ctx, empowered) =>
            hero.ReduceCooldown<StarfallVolley>(SimTime.FromSeconds(empowered ? EmpoweredReductionSeconds : ReductionSeconds), ctx.CurrentTime);
    }
}
