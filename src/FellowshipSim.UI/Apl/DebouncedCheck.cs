namespace FellowshipSim.UI.Apl;

/// <summary>
/// A component's text once it has stayed the same for a delay (<see cref="Text"/>), and the engine's check of that text
/// (<see cref="Result"/>). The first text is settled at once. <paramref name="dispatch"/> runs each change on the
/// component's renderer and re-renders it. With <paramref name="list"/>, the text is a whole action list.
/// </summary>
public sealed class DebouncedCheck(ExpressionChecks checks, TimeSpan delay, Func<Action, Task> dispatch, bool list = false) : IDisposable
{
    private string? _hero;
    private string? _latest;
    private CheckedText? _result;
    private int _version;
    private CancellationTokenSource? _timer;
    private bool _disposed;

    /// <summary>The text, once it has stayed the same for the delay.</summary>
    public string Text { get; private set; } = "";

    /// <summary>The check of <see cref="Text"/> or of an earlier text while it loads; null while <see cref="Text"/> is blank.</summary>
    public CheckedText? Result => string.IsNullOrWhiteSpace(Text) ? null : _result;

    /// <summary>Takes the component's hero and text. Call it from <c>OnParametersSet</c>.</summary>
    public void Set(string hero, string text)
    {
        if (_latest == null)
        {
            _hero = hero;
            _latest = text;
            Text = text;
            StartCheck();
            return;
        }
        if (hero != _hero)
        {
            _hero = hero;
            StartCheck();
        }
        if (text == _latest) return;
        _latest = text;
        _timer?.Cancel();
        _timer = new CancellationTokenSource();
        _ = Settle(text, _timer.Token);
    }

    private async Task Settle(string text, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await dispatch(() =>
        {
            if (_disposed || ct.IsCancellationRequested || text == Text) return;
            Text = text;
            StartCheck();
        });
    }

    private void StartCheck()
    {
        int version = ++_version;
        string text = Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        var check = list ? checks.GetList(_hero!, text) : checks.Get(_hero!, text);
        if (check.IsCompletedSuccessfully)
        {
            _result = new CheckedText(text, check.Result);
            return;
        }
        _ = Finish(check, text, version);
    }

    private async Task Finish(Task<Core.APL.AplExpressionCheck?> check, string text, int version)
    {
        var result = await check;
        await dispatch(() =>
        {
            if (!_disposed && version == _version) _result = new CheckedText(text, result);
        });
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Cancel();
    }
}
