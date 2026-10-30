using System.Globalization;

namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// A number of enemy deaths: <see cref="Value"/> enemies, or <see cref="Value"/> percent of the enemies counted when
/// <see cref="Percent"/> is true. The text is <c>3</c> or <c>50%</c>.
/// </summary>
public readonly record struct DeathCount(double Value, bool Percent = false)
{
    /// <summary>The deaths among <paramref name="enemies"/> enemies: a percent rounds up to a whole enemy.</summary>
    public int Of(int enemies) => Percent ? (int)Math.Ceiling(enemies * Value / 100.0 - 1e-9) : (int)Value;

    /// <summary>The problem with the value, or null when it is valid.</summary>
    public string? Problem() => Percent
        ? Value > 0 && Value <= 100 ? null : "a death percent is more than 0 and at most 100"
        : Value >= 1 && Value == Math.Floor(Value) ? null : "a death count is a whole number of at least 1";

    public override string ToString() =>
        Value.ToString(CultureInfo.InvariantCulture) + (Percent ? "%" : "");

    /// <summary>The death count <paramref name="text"/> writes.</summary>
    public static DeathCount Parse(string key, string text)
    {
        bool percent = text.EndsWith('%');
        string number = percent ? text[..^1] : text;
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            ? new DeathCount(n, percent)
            : throw new FormatException($"{key} is a number of enemies or a percent, not '{text}'");
    }
}
