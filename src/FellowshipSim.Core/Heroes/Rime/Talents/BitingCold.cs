using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 51 / effect 1372: the power of your critical strikes is increased (CritMultiplier).</summary>
public partial class BitingCold : Talent
{
    public const int EffectId = 1372;

    public static readonly AuraDef Passive = AuraDef.Effect(TalentName, EffectId, modifiers: StatModifier.More(StatType.CritMultiplier, Constants.MultistrikePassiveCritStrikePowerIncrease));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.PassiveAuras.Add(Passive);
}
