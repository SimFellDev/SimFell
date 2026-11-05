namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A raw fight's UTF-8 JSON for <see cref="ReplayScriptBuilder"/>, which empties the holder. Once the script is built,
/// nothing keeps the raw fight through the replay: in the browser app's worker, memory is freed only once no frame holds it.
/// </summary>
public sealed class RawFightHolder(ReadOnlyMemory<byte> rawFight)
{
    private ReadOnlyMemory<byte> _rawFight = rawFight;

    /// <summary>The raw fight, which the holder no longer has.</summary>
    public ReadOnlyMemory<byte> Take()
    {
        var rawFight = _rawFight;
        _rawFight = default;
        return rawFight;
    }
}
