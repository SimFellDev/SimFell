namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 46: "Standing still for a time grants you Patient Soul, increasing your Max Health and your Expertise
/// rating. Patient Soul persists for a time after you start moving." The engine does not
/// simulate movement: a sim keeps it up, a replay follows the log (situational aura). UNVERIFIED: no log has this sigil;
/// the game data has two Patient Soul effects (2149, 2150) and the first is assumed.
/// </summary>
public partial class PatientSoul : Sigil
{
    public const int BuffEffectId = 2149;

    public static readonly AuraDef Buff = AuraDef.Effect("Patient Soul", BuffEffectId);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero) =>
        hero.SituationalAuras.Add(Buff.WithModifiers(StatModifier.Add(StatType.ExpertiseRating, ForRank(BuffAdditionalExpertiseRatingByRank))));
}
