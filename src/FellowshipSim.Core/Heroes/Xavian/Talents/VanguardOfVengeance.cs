namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 580: "Each time you Parry an attack you automatically counter-attack that enemy for 116% Strength physical
/// damage and gain 0.5% Mana. Each successful Parry also grants you the Vanguard of Vengeance buff for 20 seconds, stacking
/// up to 20 times - increasing your Spirit by +0.5% per stack. Your Parry chance is increased by +10%." Effect 2652 (20s,
/// 20 stacks, Spirit +0.005 each). Its stacks come from Parries, so the buff is a situational aura: a replay follows the
/// log's stacks.
/// </summary>
public class VanguardOfVengeance : Talent
{
    public const int TalentId = 580;
    public const string TalentName = "Vanguard of Vengeance";
    public const int BuffEffectId = 2652;
    public const double BuffSeconds = 20.0;
    public const int MaxStacks = 20;
    public const double SpiritPerStack = 0.005;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, MaxStacks,
        StatModifier.Add(StatType.Spirit, SpiritPerStack));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.SituationalAuras.Add(Buff);
}
