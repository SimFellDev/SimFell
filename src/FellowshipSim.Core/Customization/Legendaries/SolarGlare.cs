using FellowshipSim.Core.Heroes.Xavian;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 605: "Enemies hit by your Solar Blades deal 20% reduced damage to you for 5 seconds. Solar Blades has a
/// 30% chance to have +100% additional critical strike chance." Effect 2727 "Blinding Horizon" on each enemy hit. Combat
/// logs (XyGpcHjNaAzgk7mW fight 20 @ 11.121): every enemy Solar Blades hits gets Blinding Horizon.
/// </summary>
public partial class SolarGlare : Legendary
{
    public const string ProcKey = "solar_glare";
    public const int DebuffEffectId = 2727;

    public static readonly AuraDef Debuff = AuraDef.Effect("Blinding Horizon", DebuffEffectId, Constants.DamageReductionDuration);

    public override void Bind(Hero hero)
    {
        if (hero is not Xavian xavian) return;
        var solarBlades = xavian.SolarBlades;
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != solarBlades || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Constants.GrievousCritChanceProcChance, ctx)) cast.CritChance += Constants.GrievousCritChanceCritChanceIncrease;
        };
        solarBlades.Struck += (ctx, struck) =>
        {
            for (int i = 0; i < struck.Count; i++) struck[i].Auras.Apply(Debuff, ctx.CurrentTime);
        };
    }
}
