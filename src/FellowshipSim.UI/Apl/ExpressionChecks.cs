using FellowshipSim.Core.APL;
using FellowshipSim.Engine;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// The engine's checks of expressions, each asked for once for each hero and text while the editor is open. A failed
/// check, or a hero the engine does not have, is null.
/// </summary>
public sealed class ExpressionChecks(ISimEngine engine)
{
    private readonly Dictionary<(string Hero, string Text), Task<AplExpressionCheck?>> _checks = [];

    public Task<AplExpressionCheck?> Get(string hero, string text)
    {
        lock (_checks)
        {
            if (!_checks.TryGetValue((hero, text), out var check)) _checks[(hero, text)] = check = Load(hero, text);
            return check;
        }
    }

    private async Task<AplExpressionCheck?> Load(string hero, string text)
    {
        try
        {
            return await engine.CheckExpressionAsync(hero, text);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
