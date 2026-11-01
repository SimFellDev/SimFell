namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 390: "The absorb shield from your Empowered Shield Slams takes 20% reduced damage &amp; enemies that attack you while
/// it is active take 100% Strength physical damage." The engine has no attacks on Helena, so nothing is reflected.
/// </summary>
public class UnscarredSoul : Talent
{
    public const int TalentId = 390;
    public const string TalentName = "Unscarred Soul";
    public const double ReflectCoefficient = 1.0;
    public const double AbsorbDamageTaken = 0.8;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
