using System.Text.Json;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// One raw fight event, decoded once: the values the replay script reads, and the values it copies as the log has them
/// (<see cref="RawSlice"/>). A missing value and JSON null are none. A value that is no number reads as 0, and an ID or
/// instance that is a string or a boolean reads as 0.
/// </summary>
internal struct RawEvent
{
    /// <summary>The event's text in the raw fight's bytes.</summary>
    public int Start, Length;
    public RawEventType Type;
    /// <summary>The <c>type</c> string; none for a type that is no string.</summary>
    public RawSlice TypeText;
    public double Timestamp;
    public int? SourceId, TargetId;
    public int SourceInstance, TargetInstance;
    public double AbilityGameId;
    public RawSlice AbilityGameIdText;
    public int? HitType;
    public RawSlice HitTypeText;
    public RawSlice Amount, UnmitigatedAmount, Absorbed, Blocked, Overkill, Stack, Duration, ExtraAbilityGameId, Affixes;
    public bool Tick, Fake, Activation;
    public RawUnitResources SourceResources, TargetResources;

    public readonly bool IsAura => Type >= RawEventType.ApplyBuff;

    /// <summary>
    /// The event at the reader's <see cref="JsonTokenType.StartObject"/>, at <paramref name="offset"/> in the raw fight's
    /// bytes. The reader ends at the event's <see cref="JsonTokenType.EndObject"/>.
    /// </summary>
    public static RawEvent Read(ref Utf8JsonReader reader, int offset)
    {
        var e = new RawEvent { Start = offset + (int)reader.TokenStartIndex, SourceInstance = 1, TargetInstance = 1 };
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals("timestamp"u8)) { reader.Read(); e.Timestamp = Num(ref reader); }
            else if (reader.ValueTextEquals("type"u8)) { reader.Read(); ReadType(ref reader, offset, ref e); }
            else if (reader.ValueTextEquals("sourceID"u8)) { reader.Read(); e.SourceId = Int(ref reader); }
            else if (reader.ValueTextEquals("targetID"u8)) { reader.Read(); e.TargetId = Int(ref reader); }
            else if (reader.ValueTextEquals("sourceInstance"u8)) { reader.Read(); e.SourceInstance = Int(ref reader) ?? 1; }
            else if (reader.ValueTextEquals("targetInstance"u8)) { reader.Read(); e.TargetInstance = Int(ref reader) ?? 1; }
            else if (reader.ValueTextEquals("abilityGameID"u8))
            {
                reader.Read();
                e.AbilityGameId = Num(ref reader);
                e.AbilityGameIdText = RawSlice.Read(ref reader, offset);
            }
            else if (reader.ValueTextEquals("sourceResources"u8)) { reader.Read(); e.SourceResources = RawUnitResources.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("targetResources"u8)) { reader.Read(); e.TargetResources = RawUnitResources.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("hitType"u8))
            {
                reader.Read();
                e.HitType = Int(ref reader);
                e.HitTypeText = RawSlice.Read(ref reader, offset);
            }
            else if (reader.ValueTextEquals("amount"u8)) { reader.Read(); e.Amount = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("unmitigatedAmount"u8)) { reader.Read(); e.UnmitigatedAmount = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("absorbed"u8)) { reader.Read(); e.Absorbed = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("blocked"u8)) { reader.Read(); e.Blocked = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("overkill"u8)) { reader.Read(); e.Overkill = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("tick"u8)) { reader.Read(); e.Tick = reader.TokenType == JsonTokenType.True; }
            else if (reader.ValueTextEquals("fake"u8)) { reader.Read(); e.Fake = reader.TokenType == JsonTokenType.True; }
            else if (reader.ValueTextEquals("activation"u8)) { reader.Read(); e.Activation = reader.TokenType == JsonTokenType.True; }
            else if (reader.ValueTextEquals("stack"u8)) { reader.Read(); e.Stack = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("duration"u8)) { reader.Read(); e.Duration = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("extraAbilityGameID"u8)) { reader.Read(); e.ExtraAbilityGameId = RawSlice.Read(ref reader, offset); }
            else if (reader.ValueTextEquals("affixes"u8)) { reader.Read(); e.Affixes = RawSlice.Read(ref reader, offset); }
            else reader.Read();
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.Skip();
        }
        e.Length = offset + (int)reader.BytesConsumed - e.Start;
        return e;
    }

    private static void ReadType(ref Utf8JsonReader reader, int offset, ref RawEvent e)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            e.Type = RawEventType.Other;
            e.TypeText = default;
            return;
        }
        e.TypeText = RawSlice.Read(ref reader, offset);
        e.Type = reader.ValueTextEquals("damage"u8) ? RawEventType.Damage
            : reader.ValueTextEquals("cast"u8) ? RawEventType.Cast
            : reader.ValueTextEquals("begincast"u8) ? RawEventType.BeginCast
            : reader.ValueTextEquals("applybuff"u8) ? RawEventType.ApplyBuff
            : reader.ValueTextEquals("applybuffstack"u8) ? RawEventType.ApplyBuffStack
            : reader.ValueTextEquals("removebuff"u8) ? RawEventType.RemoveBuff
            : reader.ValueTextEquals("removebuffstack"u8) ? RawEventType.RemoveBuffStack
            : reader.ValueTextEquals("refreshbuff"u8) ? RawEventType.RefreshBuff
            : reader.ValueTextEquals("applydebuff"u8) ? RawEventType.ApplyDebuff
            : reader.ValueTextEquals("applydebuffstack"u8) ? RawEventType.ApplyDebuffStack
            : reader.ValueTextEquals("removedebuff"u8) ? RawEventType.RemoveDebuff
            : reader.ValueTextEquals("removedebuffstack"u8) ? RawEventType.RemoveDebuffStack
            : reader.ValueTextEquals("refreshdebuff"u8) ? RawEventType.RefreshDebuff
            : reader.ValueTextEquals("beginchannel"u8) ? RawEventType.BeginChannel
            : reader.ValueTextEquals("endchannel"u8) ? RawEventType.EndChannel
            : reader.ValueTextEquals("death"u8) ? RawEventType.Death
            : reader.ValueTextEquals("resurrect"u8) ? RawEventType.Resurrect
            : reader.ValueTextEquals("combatantinfo"u8) ? RawEventType.CombatantInfo
            : reader.ValueTextEquals("dungeonstart"u8) ? RawEventType.DungeonStart
            : RawEventType.Other;
    }

    /// <summary>The reader's number; 0 for a value that is no number.</summary>
    public static double Num(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out double d) ? d : 0;

    /// <summary>The reader's value as an ID: a number cast to int, 0 for a string or a boolean, null for anything else.</summary>
    public static int? Int(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.Number => (int)Num(ref reader),
        JsonTokenType.String or JsonTokenType.True or JsonTokenType.False => 0,
        _ => null,
    };
}
