namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 751: "Your Rend ticks faster, dealing more damage." Combat logs (n7zxK8harZT2wjDB fight 9,
/// Y4wpb2f3gc8LqJDX fight 25): ticks come on whole server frames, and each application deals one more tick.
/// </summary>
public partial class DeepRend : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Gunde)hero).Rend.TickRate = Constants.ReducedTickSpeedPeriodFrequencyIncrease;
}
