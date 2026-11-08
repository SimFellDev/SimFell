namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 567: "Each time you successfully Parry an attack you are granted 1 stack of Solar Vanguard for 30 seconds. Solar
/// Vanguard increases the power of your next Solar Shield by 1% per stack. Stacks up to 30 times. Solar Shield consumes
/// all stacks of Solar Vanguard." Effect 2649 "Hallowed Shield" (30s, 30 stacks). A Parry grants the stack, so it is a
/// defensive proc: a replay fires it at each stack the log gains.
/// </summary>
public partial class HallowedShield : Talent
{
    public const int BuffEffectId = 2649;
    public const string ProcKey = "hallowed_shield_parry";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, Duration, StackCap);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var shield = ((Xavian)hero).SolarShield;
        hero.AddDefensiveProc(ProcKey, (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime));
        shield.AbsorbMultipliers.Add(ctx => 1.0 + ExtraAbsorbPerStack * hero.Auras.GetStacks(Buff, ctx.CurrentTime));
        shield.Shielded += (ctx, _) => hero.Auras.Remove(Buff, ctx.CurrentTime);
    }
}
