using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.UI.Apl;

namespace FellowshipSim.Tests.Apl.Editor;

/// <summary>Every value, property, scope and operator a condition reads has a label and a meaning for the editor and the condition reference.</summary>
public class ConditionDocsTests
{
    private readonly AplVocabulary _v = EditorVocabulary.Create();

    private static void AssertDoc(AplDoc? doc, string what)
    {
        Assert.True(doc is { Label.Length: > 0, Description.Length: > 0 }, $"{what} has no label and meaning.");
    }

    [Fact]
    public void Every_scope_property_has_a_doc()
    {
        foreach (var (scope, properties) in PropertyExpression.Scopes)
        {
            AssertDoc(PropertyExpression.ScopeDocs.GetValueOrDefault(scope), scope);
            foreach (string property in properties) AssertDoc(PropertyExpression.PropertyDoc(scope, property), $"{scope}.<name>.{property}");
        }
        AssertDoc(PropertyExpression.ScopeDocs.GetValueOrDefault(PropertyExpression.ActiveDotScope), PropertyExpression.ActiveDotScope);
        AssertDoc(PropertyExpression.ScopeDocs.GetValueOrDefault(PropertyExpression.VariableScope), PropertyExpression.VariableScope);
    }

    [Fact]
    public void Every_named_value_action_value_and_build_scope_has_a_doc()
    {
        foreach (var (path, named) in PropertyExpression.Named) AssertDoc(named.Doc, path);
        foreach (string path in PropertyExpression.ActionValues.Keys) AssertDoc(PropertyExpression.ActionValueDoc(path), path);
        foreach (var (scope, build) in PropertyExpression.BuildScopes) AssertDoc(build.Doc, scope);
    }

    [Fact]
    public void Every_operator_has_a_doc()
    {
        foreach (var op in Enum.GetValues<BinaryOperator>()) AssertDoc(op.Doc(), op.ToString());
        foreach (var op in Enum.GetValues<UnaryOperator>()) AssertDoc(op.Doc(), op.ToString());
    }

    [Fact]
    public void Every_kind_and_property_in_the_builder_has_a_doc()
    {
        foreach (var kind in ConditionBuilder.Kinds(_v, []))
        {
            Assert.False(string.IsNullOrEmpty(kind.Description), $"{kind.Id} has no meaning.");
            foreach (string property in kind.Properties ?? []) AssertDoc(ConditionBuilder.PropertyDoc(kind.Id, property), $"{kind.Id}.{property}");
        }
    }

    [Fact]
    public void An_action_value_reads_as_the_scoped_property_it_stands_for()
    {
        var doc = PropertyExpression.ActionValueDoc("charges_fractional")!;

        Assert.Equal(PropertyExpression.PropertyDoc(PropertyExpression.CooldownScope, "charges_fractional")!.Label, doc.Label);
        Assert.StartsWith("The same value as cooldown.<this action>.charges_fractional. ", doc.Description);
    }
}
