using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>Talent 575: "Your Omega Reprieval cooldown is reduced by 3 seconds each time you cast Brilliant Flash."</summary>
public partial class CelestialFavor : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        ((Xavian)hero).BrilliantFlash.Flashed += (ctx, _) =>
            hero.ReduceCooldown<OmegaReprieval>(SimTime.FromSeconds(Constants.ActiveCooldownReductionCooldownReduction), ctx.CurrentTime);
}
