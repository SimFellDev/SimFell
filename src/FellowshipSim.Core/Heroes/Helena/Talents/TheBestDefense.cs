using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 207: "Each time you Block an attack with your shield, you have a chance to reset the cooldown of Shield Throw and
/// cause your next Shield Throw within 12 seconds to deal 20% increased damage. (2.0 PPM)" The buff is effect 1231. The
/// engine has no attacks on Helena, so nothing blocks and nothing procs it: the next Shield Throw uses up a buff applied
/// some other way.
/// </summary>
public class TheBestDefense : Talent
{
    public const int TalentId = 207;
    public const string TalentName = "The Best Defense";
    public const int BuffEffectId = 1231;
    public const double BuffSeconds = 12.0;
    public const double ShieldThrowDamage = 1.2;
    public const double ProcsPerMinute = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not ShieldThrow || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.Remove(Buff, cast.Time);
            cast.DamageMultiplier *= ShieldThrowDamage;
        };
    }
}
