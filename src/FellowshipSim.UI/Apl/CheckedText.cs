using FellowshipSim.Core.APL;

namespace FellowshipSim.UI.Apl;

/// <summary>The engine's check of an expression (null when it failed), with the text it was made for.</summary>
public sealed record CheckedText(string Text, AplExpressionCheck? Check);
