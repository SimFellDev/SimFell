namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>Glyph 700: "Your [POWER] abilities grant you increased Critical Strike chance by rank for a duration."</summary>
public partial class TheTrickster : Glyph
{
    public const int BuffEffectId = 3181;

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        var aura = hero.Register(AuraDef.Effect(GlyphName, BuffEffectId, Duration,
            modifiers: StatModifier.Add(StatType.CritChance, ForRank(AdditionalCritChanceByRank))));
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability.Category == AbilityCategory.Power) hero.Auras.Apply(aura, now);
        };
    }
}
