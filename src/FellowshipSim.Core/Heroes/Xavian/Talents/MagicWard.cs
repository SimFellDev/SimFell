namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 584: "When you cast Solar Shield on yourself, it grants you Magic Ward, reducing all Magic Damage taken by 30%
/// for 4 seconds." Effect 2650 (4s). Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 1.136): applied with the shield.
/// </summary>
public partial class MagicWard : Talent
{
    public const int BuffEffectId = 2650;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, Constants.MagicDamageReductionBuffDuration);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) =>
        ((Xavian)hero).SolarShield.Shielded += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
}
