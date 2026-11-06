using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 124: Multishot reduces the cooldown of Starfall Volley. Empowered Multishot reduces the
/// cooldown by more.
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
