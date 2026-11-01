namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 567: "Each time you successfully Parry an attack you are granted 1 stack of Solar Vanguard for 30 seconds. Solar
/// Vanguard increases the power of your next Solar Shield by 1% per stack. Stacks up to 30 times. Solar Shield consumes
/// all stacks of Solar Vanguard." Effect 2649 "Hallowed Shield" (30s, 30 stacks). Its stacks come from Parries, so they
/// are a situational aura: a replay follows the log's stacks, and the log removes them as a Solar Shield is cast.
/// </summary>
public class HallowedShield : Talent
{
    public const int TalentId = 567;
    public const string TalentName = "Hallowed Shield";
    public const int BuffEffectId = 2649;
    public const double BuffSeconds = 30.0;
    public const int MaxStacks = 30;
    public const double AbsorbPerStack = 0.01;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.SituationalAuras.Add(Buff);
        ((Xavian)hero).SolarShield.AbsorbMultipliers.Add(ctx => 1.0 + AbsorbPerStack * hero.Auras.GetStacks(Buff, ctx.CurrentTime));
    }
}
