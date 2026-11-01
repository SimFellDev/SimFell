using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 568: "Your Omnistrike and Solar Blades abilities grant you 1 stack of Light's Oath for 30 seconds, stacking up
/// to 8 times. When you have 8 stacks of Light's Oath, your next Shining Halo deals +100% increased damage." Effect 2655
/// (30s, 8 stacks; its own text gives 200% more). UNVERIFIED: no combat log has this talent.
/// </summary>
public class LightsOath : Talent
{
    public const int TalentId = 568;
    public const string TalentName = "Light's Oath";
    public const int BuffEffectId = 2655;
    public const double BuffSeconds = 30.0;
    public const int MaxStacks = 8;
    public const double HaloDamageMultiplier = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        xavian.GetAbility<Omnistrike>()!.Struck += ctx => hero.Auras.Apply(Buff, ctx.CurrentTime);
        xavian.SolarBlades.Struck += (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime);
        xavian.ShiningHalo.CastDamageMultipliers.Add(ctx => hero.Auras.GetStacks(Buff, ctx.CurrentTime) >= MaxStacks ? HaloDamageMultiplier : 1.0);
        xavian.ShiningHalo.Placed += ctx =>
        {
            if (hero.Auras.GetStacks(Buff, ctx.CurrentTime) >= MaxStacks) hero.Auras.Remove(Buff, ctx.CurrentTime);
        };
    }
}
