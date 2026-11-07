using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 617: each Detonate cast extends the duration of the hero's active Searing Blaze effects, up to the maximum
/// duration. UNVERIFIED: no combat log has this talent.
/// </summary>
public class Backdraft : Talent
{
    public const int TalentId = 617;
    public const string TalentName = "Backdraft";
    public const double ExtensionSeconds = 1.5;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var searing = ardeos.GetAbility<SearingBlaze>()?.Dot;
        if (searing == null) return;
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (ability is not Detonate || hero.SimContext is not { } ctx) return;
            foreach (var dot in ardeos.Dots.Active.Where(d => d.Effect == searing).ToList())
            {
                ardeos.Dots.Extend(ctx, dot, ExtensionSeconds, maxRemainingSeconds: searing.Duration);
            }
        };
    }
}
