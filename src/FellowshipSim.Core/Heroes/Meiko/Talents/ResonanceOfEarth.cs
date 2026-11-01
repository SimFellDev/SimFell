using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 90: "When you activate Stone Shield you take 20% reduced damage for 8 seconds." Effect 1512: IncomingDamageMultiplier
/// x0.8 (Meiko.DamageSoakBuff.Talent.IncomingDamageMultiplier.DamageMultiplier 0.8, Duration 8).
/// </summary>
public class ResonanceOfEarth : Talent
{
    public const int TalentId = 90;
    public const string TalentName = "Resonance of Earth";
    public const int BuffEffectId = 1512;
    public const double BuffSeconds = 8.0;
    public const double IncomingDamage = 0.8;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.IncomingDamageMultiplier, IncomingDamage));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is StoneShield) hero.Auras.Apply(Buff, now);
        };
    }
}
