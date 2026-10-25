using FellowshipSim.Core.APL;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>A vocabulary as the editor reads it from JSON: two abilities, a resource with an alias, and two auras.</summary>
public static class EditorVocabulary
{
    public static AplVocabulary Create() => new(
        "Hero",
        [new AplName("frost_bolt", "Frost Bolt"), new AplName("ice_comet", "Ice Comet")],
        [new AplName("winter_orbs", "Winter Orbs"), new AplName("worb", "Winter Orbs")],
        [new AplName("chill", "Chill"), new AplName("glacial_assault", "Glacial Assault")],
        ["frost_bolt"]);
}
