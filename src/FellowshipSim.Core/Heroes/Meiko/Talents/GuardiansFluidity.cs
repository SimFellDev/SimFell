using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 514: "Your Finishing Moves reduce the cooldown of your Stone Shield ability by 1 second"
/// (Meiko.DamageSoakBuff.Talent.FinisherToReducedCooldown.CooldownReductionInSeconds 1).
/// </summary>
public partial class GuardiansFluidity : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Meiko meiko) return;
        meiko.Finished += (ctx, _, _) =>
            hero.ReduceCooldown<StoneShield>(SimTime.FromSeconds(CooldownReductionInSeconds), ctx.CurrentTime);
    }
}
