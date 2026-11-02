using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 107: "Your Finishers deal 10% more damage" (Meiko.Finishers.Talent.IncreasedDamageScaler 1.1). The game data's
/// changes list names Rising Storm, Rising Earth, Double Palm Strike, Lashing Stormkick, Earthfist Barrage (its damage and
/// its crit chance) and Spirited Vortex. It raises Rising Storm, Rising Earth, Double Palm Strike, Lashing Stormkick and
/// Spirited Vortex: combat logs (16 fights) match those finishers within 1.2% with the 10%, and put Earthfist Barrage's
/// hits at 0.85-0.95 of the engine's with the 10% for the 12 players with the talent, and 0.99-1.01 for the players
/// without it.
/// </summary>
public class ConclusiveStrikes : Talent
{
    public const int TalentId = 107;
    public const string TalentName = "Conclusive Strikes";
    public const double DamageMultiplier = 1.1;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        foreach (var finisher in hero.Abilities.OfType<MeikoFinisher>())
        {
            if (finisher is not EarthfistBarrage) finisher.AddDamageMultiplier(DamageMultiplier);
        }
    }
}
