using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 572: "Your Sky Crash increases your movement speed by 20% and your Armor by 40% for 18 seconds." Effect 2646
/// (18s). No damage. Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 10.222): applied as the crash lands, for 18s.
/// </summary>
public class TemperedDescent : Talent
{
    public const int TalentId = 572;
    public const string TalentName = "Tempered Descent";
    public const int BuffEffectId = 2646;
    public const double BuffSeconds = 18.0;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        hero.GetAbility<SkyCrash>()!.Landed += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
}
