using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>Talent 575: "Your Omega Reprieval cooldown is reduced by 3 seconds each time you cast Brilliant Flash."</summary>
public class CelestialFavor : Talent
{
    public const int TalentId = 575;
    public const string TalentName = "Celestial Favor";
    public const double CooldownReductionSeconds = 3.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        ((Xavian)hero).BrilliantFlash.Flashed += (ctx, _) =>
            hero.ReduceCooldown<OmegaReprieval>(SimTime.FromSeconds(CooldownReductionSeconds), ctx.CurrentTime);
}
