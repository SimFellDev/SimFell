using FellowshipSim.Core.APL;
using FellowshipSim.Engine;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The check of a .simc file: the tokens of its action list and its profile lines, the check of its action list (null with
/// no hero the engine has), and its profile's problems.
/// </summary>
public sealed record SimcCheck(IReadOnlyList<AplTokenSpan> Tokens, AplExpressionCheck? ActionList, IReadOnlyList<SimProfileProblem> Problems);
