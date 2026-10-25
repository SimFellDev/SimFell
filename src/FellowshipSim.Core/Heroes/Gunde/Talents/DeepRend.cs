namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 751: "Your Rend ticks 10% faster, dealing 10% more damage." Combat logs (n7zxK8harZT2wjDB fight 9,
/// Y4wpb2f3gc8LqJDX fight 25): ticks every 82 server frames (2.733s), 11 ticks of a tenth of each application.
/// </summary>
public class DeepRend : Talent
{
    public const int TalentId = 751;
    public const string TalentName = "Deep Rend";
    public const double TickRate = 1.1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Gunde)hero).Rend.TickRate = TickRate;
}
