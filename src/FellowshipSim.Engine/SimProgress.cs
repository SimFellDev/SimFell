using System.Globalization;
using System.Text.Json.Serialization;

namespace FellowshipSim.Engine;

/// <summary>
/// Completed fights of a Monte Carlo sim of <see cref="Fights"/> fights. A sim to a <see cref="TargetError"/> has its
/// margin of error so far in <see cref="Error"/> (both in percent of the mean DPS), and <see cref="Fights"/> is its most
/// fights.
/// </summary>
public sealed record SimProgress(int CompletedFights, int Fights, double? Error = null, double? TargetError = null)
{
    /// <summary>
    /// The share of the sim done. A sim to a target error has the larger of its share of the most fights and its share of
    /// the fights the target needs: the error falls with the square root of the fights.
    /// </summary>
    [JsonIgnore]
    public double Fraction
    {
        get
        {
            double fights = Fights == 0 ? 1 : (double)CompletedFights / Fights;
            return TargetError is > 0 and var target && Error is > 0 and var error && double.IsFinite(error)
                ? Math.Max(fights, Math.Min(1, target * target / (error * error)))
                : fights;
        }
    }

    /// <summary>The fights done, and with a target error the error so far.</summary>
    [JsonIgnore]
    public string Text => TargetError is > 0 and var target
        ? string.Format(CultureInfo.InvariantCulture, "{0:N0} fights, error {1}, target {2:0.###}%", CompletedFights,
            Error is { } error && double.IsFinite(error) ? error.ToString("0.###", CultureInfo.InvariantCulture) + "%" : "–", target)
        : string.Format(CultureInfo.InvariantCulture, "{0:N0} of {1:N0} fights", CompletedFights, Fights);
}
