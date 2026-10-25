namespace FellowshipSim.UI.Services;

/// <summary>
/// The person using the app. A web visitor has an ID from their browser's visitor cookie, shared by every tab of that
/// browser, and has their own jobs, sims, reviews and action lists under <c>visitors/&lt;id&gt;/</c> in the
/// <see cref="IBlobStore"/>. The desktop app's user is <see cref="DataFolderOwner"/>: the data folder is theirs alone.
/// </summary>
public sealed record Visitor(string Id, bool OwnsDataFolder)
{
    /// <summary>The desktop app's user, whose files are directly in the data folder.</summary>
    public static Visitor DataFolderOwner { get; } = new("local", OwnsDataFolder: true);

    /// <summary>The <see cref="IBlobStore"/> key of <paramref name="path"/> among this visitor's files.</summary>
    public string Key(string path) => OwnsDataFolder ? path : $"visitors/{Id}/{path}";
}
