namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The damage ID, resource, aura, ability or proc a <see cref="ReplayIssue"/> is about. Every check of one entity belongs to
/// one <see cref="Finding"/>, so a fix to that entity has one place in the review.
/// </summary>
public sealed record IssueEntity(IssueEntityKind Kind, int Id, string Name)
{
    public static IssueEntity Damage(int logId, string name) => new(IssueEntityKind.DamageId, logId, name);
    public static IssueEntity Resource(string name) => new(IssueEntityKind.Resource, 0, name);
    public static IssueEntity Aura(int logId, string name) => new(IssueEntityKind.Aura, logId, name);
    public static IssueEntity Ability(string name) => new(IssueEntityKind.Ability, 0, name);
    public static IssueEntity Proc(string key) => new(IssueEntityKind.Proc, 0, key);

    /// <summary>
    /// The entity's identity: the ID where it has one, since one damage ID has several names ("Shatter Earth" and
    /// "Shatter Earth (quake)") and one name several damage IDs ("Fire Frogs"); its name otherwise.
    /// </summary>
    public (IssueEntityKind Kind, int Id, string Name) Key => (Kind, Id, Id != 0 ? "" : Name);

    /// <summary><see cref="Key"/> as text, the same in every review and every run: <c>DamageId:1001242</c>, <c>Resource:Mana</c>.</summary>
    public string KeyText => $"{Kind}:{(Id != 0 ? Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : Name)}";
}
