namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>Glyph 700: "Your [POWER] abilities grant you increased Critical Strike chance by rank for a duration."</summary>
public class TheTrickster : Glyph
{
    public const int GlyphId = 700;
    public const string GlyphName = "The Trickster";
    public const int BuffEffectId = 3181;
    public const double DurationSeconds = 4.0;
    public static readonly double[] CritChanceByRank = [0.025, 0.04, 0.064, 0.10];

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        var aura = hero.Register(AuraDef.Effect(GlyphName, BuffEffectId, DurationSeconds,
            modifiers: StatModifier.Add(StatType.CritChance, ForRank(CritChanceByRank))));
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability.Category == AbilityCategory.Power) hero.Auras.Apply(aura, now);
        };
    }
}
