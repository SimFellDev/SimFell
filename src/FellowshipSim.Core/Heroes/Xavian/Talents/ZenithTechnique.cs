using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 608: "While your Shining Halo is active, your Skycrash replaces it at the destination and extends its duration
/// by 6 seconds." Effect 2645 "Zenith Technique" (3s; Parry chance +0.2). The engine puts every enemy in the halo, before
/// and after the move. UNVERIFIED.
/// </summary>
public class ZenithTechnique : Talent
{
    public const int TalentId = 608;
    public const string TalentName = "Zenith Technique";
    public const double ExtensionSeconds = 6.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var halo = ((Xavian)hero).ShiningHalo;
        hero.GetAbility<SkyCrash>()!.Crashed += ctx => halo.Extend(ctx, ExtensionSeconds);
    }
}
