using System.Text.RegularExpressions;

namespace FellowshipSim.Logs;

/// <summary>A FellowshipLogs report, optionally narrowed to a fight and a player (from a pasted link or a bare code).</summary>
public sealed record ReportLink(string ReportCode, int? FightId, int? PlayerId)
{
    /// <summary>The report's FellowshipLogs page, with its fight and player when set.</summary>
    public string Url
    {
        get
        {
            string query = string.Join('&', new[] { FightId is { } f ? $"fight={f}" : null, PlayerId is { } p ? $"source={p}" : null }.OfType<string>());
            return $"https://www.fellowshiplogs.com/reports/{ReportCode}{(query.Length > 0 ? "?" + query : "")}";
        }
    }

    /// <summary>
    /// Reads "https://www.fellowshiplogs.com/reports/CODE?fight=7&amp;source=1" or just "CODE".
    /// </summary>
    public static ReportLink Parse(string linkOrCode)
    {
        if (string.IsNullOrWhiteSpace(linkOrCode)) throw new ArgumentException("Paste a FellowshipLogs report link or code.");
        string text = linkOrCode.Trim();
        var code = Regex.Match(text, @"reports/([A-Za-z0-9]+)");
        var fight = Regex.Match(text, @"[?&#]fight=(\d+)");
        var source = Regex.Match(text, @"[?&#]source=(\d+)");
        string reportCode = code.Success ? code.Groups[1].Value : text.Split('?', '#')[0].Trim('/');
        if (!Regex.IsMatch(reportCode, "^[A-Za-z0-9]+$")) throw new ArgumentException($"'{linkOrCode}' is not a FellowshipLogs report link or code.");
        return new ReportLink(
            reportCode,
            fight.Success ? int.Parse(fight.Groups[1].Value) : null,
            source.Success ? int.Parse(source.Groups[1].Value) : null);
    }
}
