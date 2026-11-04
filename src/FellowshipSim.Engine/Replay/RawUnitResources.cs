using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// An event's <c>sourceResources</c> or <c>targetResources</c>: the unit's health, maximum health and resources. A value
/// missing or JSON null has its <c>Has</c> flag false, and a value that is no number reads as 0.
/// </summary>
internal struct RawUnitResources
{
    public bool HasHitPoints;
    public double HitPoints;
    public bool HasMaxHitPoints;
    public double MaxHitPoints;
    public RawSlice MaxHitPointsText;
    /// <summary>The <c>resources</c> array; none for a value that is no array.</summary>
    public RawSlice Resources;

    /// <summary>The unit's resources at the reader's current value, at <paramref name="offset"/> in the raw fight's bytes.</summary>
    public static RawUnitResources Read(ref Utf8JsonReader reader, int offset)
    {
        var resources = default(RawUnitResources);
        if (reader.TokenType != JsonTokenType.StartObject) return resources;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("hitPoints"u8))
            {
                reader.Read();
                resources.HasHitPoints = reader.TokenType != JsonTokenType.Null;
                resources.HitPoints = RawEvent.Num(ref reader);
            }
            else if (reader.ValueTextEquals("maxHitPoints"u8))
            {
                reader.Read();
                resources.HasMaxHitPoints = reader.TokenType != JsonTokenType.Null;
                resources.MaxHitPoints = RawEvent.Num(ref reader);
                resources.MaxHitPointsText = RawSlice.Read(ref reader, offset);
            }
            else if (reader.ValueTextEquals("resources"u8))
            {
                reader.Read();
                resources.Resources = reader.TokenType == JsonTokenType.StartArray ? RawSlice.Read(ref reader, offset) : default;
            }
            else
            {
                reader.Read();
            }
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.Skip();
        }
        return resources;
    }
}
