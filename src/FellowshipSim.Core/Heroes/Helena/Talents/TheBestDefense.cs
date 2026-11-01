using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 207: "Each time you Block an attack with your shield, you have a chance to reset the cooldown of Shield Throw and
/// cause your next Shield Throw within 12 seconds to deal 20% increased damage. (2.0 PPM)" The buff is effect 1231. A block
/// is a defensive proc: a replay fires it at each gain of the buff in the log, a sim never.
/// </summary>
public class TheBestDefense : Talent
{
    public const int TalentId = 207;
    public const string TalentName = "The Best Defense";
    public const int BuffEffectId = 1231;
    public const double BuffSeconds = 12.0;
    public const double ShieldThrowDamage = 1.2;
    public const double ProcsPerMinute = 2.0;
    public const string ProcKey = "the_best_defense";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.AddDefensiveProc(ProcKey, (ctx, _) =>
        {
            if (hero.GetAbility<ShieldThrow>() is { } shieldThrow) hero.CooldownOf(shieldThrow).ResetAll();
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        });
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not ShieldThrow || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.Remove(Buff, cast.Time);
            cast.DamageMultiplier *= ShieldThrowDamage;
        };
    }
}
