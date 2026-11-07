using FellowshipSim.Core.Heroes.Xavian;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 605 (Xavian's Light of Azamir Breastplate, item 5898): "Enemies hit by your Solar Blades deal 20%
/// reduced damage to you for 5 seconds. Solar Blades has a 30% chance to have +100% additional critical strike chance."
/// Effect 2727 "Blinding Horizon" (5s) on each enemy hit. Combat logs (XyGpcHjNaAzgk7mW fight 20 @ 11.121): every enemy
/// Solar Blades hits gets Blinding Horizon.
/// </summary>
public class SolarGlare : Legendary
{
    public const int PowerId = 605;
    public const int BreastplateItemId = 5898;
    public const double Chance = 0.3;
    public const double CritChance = 1.0;
    public const string ProcKey = "solar_glare";
    public const int DebuffEffectId = 2727;
    public const double DebuffSeconds = 5.0;

    public static readonly AuraDef Debuff = AuraDef.Effect("Blinding Horizon", DebuffEffectId, DebuffSeconds);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Solar Glare";
    public override string HeroName => "Xavian";
    public override IReadOnlyList<int> ItemIds => [BreastplateItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Xavian xavian) return;
        var solarBlades = xavian.SolarBlades;
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != solarBlades || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) cast.CritChance += CritChance;
        };
        solarBlades.Struck += (ctx, struck) =>
        {
            for (int i = 0; i < struck.Count; i++) struck[i].Auras.Apply(Debuff, ctx.CurrentTime);
        };
    }
}
