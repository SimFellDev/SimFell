namespace FellowshipSim.Logs;

/// <summary>
/// The events an events query of FellowshipLogs returns: from <see cref="SourceId"/> (0 for every source),
/// <see cref="DataType"/> (an <c>EventDataType</c>), <see cref="HostilityType"/> (<c>Friendlies</c> or <c>Enemies</c>) and
/// <see cref="FilterExpression"/> (empty for every event).
/// </summary>
internal sealed record EventStream(int SourceId, string DataType, string HostilityType, string FilterExpression);
