namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 46: "Standing still for a time grants you Patient Soul, increasing your Max Health and your Expertise
/// rating. Patient Soul persists for a time after you start moving." The engine does not
/// simulate movement: a sim keeps it up, a replay follows the log (situational aura). The game data has two Patient Soul
/// effects (2149, 2150), both MaxHealth MultiplyCompound <see cref="MaxHealthMultiplier"/>; combat logs show 2150 (an
/// Ardeos combatantinfo of RawGearStatsTests, whose logged health has it).
/// </summary>
public partial class PatientSoul : Sigil
{
    public const int BuffEffectId = 2150;
    /// <summary>Max health, every rank (game data effects 2149 and 2150).</summary>
    public const double MaxHealthMultiplier = 1.05;

    public static readonly AuraDef Buff = AuraDef.Effect("Patient Soul", BuffEffectId);

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero) =>
        hero.SituationalAuras.Add(Buff.WithModifiers(StatModifier.Add(StatType.ExpertiseRating, ForRank(Constants.BuffAdditionalExpertiseRatingByRank)),
            StatModifier.More(StatType.MaxHealth, MaxHealthMultiplier)));
}
