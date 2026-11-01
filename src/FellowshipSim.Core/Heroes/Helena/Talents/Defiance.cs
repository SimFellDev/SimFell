namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 648: "When you are below 80% health, you gain +5% Block Chance and 20% increased Healing taken." The buff is
/// effect 3094. It depends on Helena's health, which the engine does not simulate: a situational aura. No damage changes.
/// </summary>
public class Defiance : Talent
{
    public const int TalentId = 648;
    public const string TalentName = "Defiance";
    public const int BuffEffectId = 3094;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.SituationalAuras.Add(Buff);
}
