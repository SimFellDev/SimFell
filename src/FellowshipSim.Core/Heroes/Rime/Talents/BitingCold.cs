using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 51 / effect 1372: the power of your critical strikes is increased (CritMultiplier).</summary>
public class BitingCold : Talent
{
    public const int TalentId = 51;
    public const string TalentName = "Biting Cold";
    public const int EffectId = 1372;
    public const double CritPower = 1.1;

    public static readonly AuraDef Passive = AuraDef.Effect(TalentName, EffectId, modifiers: StatModifier.More(StatType.CritMultiplier, CritPower));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.PassiveAuras.Add(Passive);
}
