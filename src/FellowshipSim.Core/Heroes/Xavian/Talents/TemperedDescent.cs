using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 572: "Your Sky Crash increases your movement speed by 20% and your Armor by 40% for 18 seconds." Effect 2646
/// (18s). Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 10.222): applied with the crash's damage, for 18s.
/// </summary>
public partial class TemperedDescent : Talent
{
    public const int BuffEffectId = 2646;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, ProcDuration);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        hero.GetAbility<SkyCrash>()!.Crashed += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
}
