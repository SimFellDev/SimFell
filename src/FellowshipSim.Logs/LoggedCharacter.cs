namespace FellowshipSim.Logs;

/// <summary>A FellowshipLogs character: its ID, its name and the fights of its recent reports, the latest first.</summary>
public sealed record LoggedCharacter(int Id, string Name, IReadOnlyList<CharacterFight> Fights);
