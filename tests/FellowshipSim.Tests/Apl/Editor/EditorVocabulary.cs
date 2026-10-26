using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>
/// A vocabulary as the editor reads it from JSON: two abilities, a resource with an alias, two auras, and a talent and a
/// weapon (the other build scopes have no names).
/// </summary>
public static class EditorVocabulary
{
    public static AplVocabulary Create() => new(
        "Hero",
        [new AplName("frost_bolt", "Frost Bolt"), new AplName("ice_comet", "Ice Comet")],
        [new AplName("winter_orbs", "Winter Orbs"), new AplName("worb", "Winter Orbs")],
        [new AplName("chill", "Chill"), new AplName("glacial_assault", "Glacial Assault")],
        ["frost_bolt"],
        PropertyExpression.BuildScopes.Keys.ToDictionary(scope => scope, scope => (IReadOnlyList<AplName>)(scope switch
        {
            PropertyExpression.TalentScope => [new AplName("icy_talons", "Icy Talons")],
            PropertyExpression.WeaponScope => [new AplName("chronoshift", "Chronoshift")],
            _ => [],
        })));
}
