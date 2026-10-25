namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 46: "Standing still for 3 seconds grants you Patient Soul, increasing your Max Health by 5% and your Expertise
/// rating by 12 / 22 / 32 / 42. Patient Soul persists for 6 seconds after you start moving." The engine does not
/// simulate movement: a sim keeps it up, a replay follows the log (situational aura). UNVERIFIED: no log has this sigil;
/// the game data has two Patient Soul effects (2149, 2150) and the first is assumed.
/// </summary>
public class PatientSoul : Sigil
{
    public const int SigilId = 46;
    public const string SigilName = "Patient Soul";
    public const int BuffEffectId = 2149;
    public static readonly double[] ExpertiseRatingByRank = [12, 22, 32, 42];

    public static readonly AuraDef Buff = AuraDef.Effect("Patient Soul", BuffEffectId);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero) =>
        hero.SituationalAuras.Add(Buff.WithModifiers(StatModifier.Add(StatType.ExpertiseRating, ForRank(ExpertiseRatingByRank))));
}
