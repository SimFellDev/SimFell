using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>Talent 210: "Your Hold the Line cooldown is reduced by 6 seconds each time you use Shields Up."</summary>
public class HighCommand : Talent
{
    public const int TalentId = 210;
    public const string TalentName = "High Command";
    public const double HoldTheLineReductionSeconds = 6.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ShieldsUp>() is not { } shieldsUp || hero.GetAbility<HoldTheLine>() is not { } holdTheLine) return;
        shieldsUp.Raised += ctx => hero.CooldownOf(holdTheLine).Reduce(SimTime.FromSeconds(HoldTheLineReductionSeconds), ctx.CurrentTime);
    }
}
