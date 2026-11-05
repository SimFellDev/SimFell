using System.Text.RegularExpressions;

namespace FellowshipSim.Logs;

/// <summary>A FellowshipLogs character's page, by the character's ID.</summary>
public sealed partial record CharacterLink(int CharacterId)
{
    /// <summary>The character's page.</summary>
    public string Url => $"https://www.fellowshiplogs.com/character/id/{CharacterId}";

    /// <summary>Reads "https://www.fellowshiplogs.com/character/id/615"; null for other text.</summary>
    public static CharacterLink? TryParse(string text)
    {
        var match = PagePattern().Match(text.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, out int id) ? new CharacterLink(id) : null;
    }

    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?fellowshiplogs\.com/character/id/(\d+)/?(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex PagePattern();
}
