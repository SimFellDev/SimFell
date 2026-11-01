namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 219: "When your Toughness has reduced an amount of damage equal to 30% of your Maximum Health, you gain 20%
/// increased Strength for 14 sec." The buff is effect 1215 (Strength x1.2); its build-up is effect 1214. It depends on the
/// damage Helena takes, which the engine does not simulate: a situational aura, up all fight in a sim and following the
/// log in a replay.
/// </summary>
public class SecondWind : Talent
{
    public const int TalentId = 219;
    public const string TalentName = "Second Wind";
    public const int BuffEffectId = 1215;
    public const double BuffSeconds = 14.0;
    public const double Strength = 1.2;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, Strength));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.SituationalAuras.Add(Buff);
}
