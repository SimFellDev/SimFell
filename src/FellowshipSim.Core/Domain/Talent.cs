namespace FellowshipSim.Core.Domain;

public abstract class Talent
{
    public abstract string Name { get; }
    public abstract int Id { get; }
    public virtual string Description => string.Empty;

    /// <summary>The talent's icon URL from the game data (the generated <c>TalentIcon</c>), or null.</summary>
    public virtual string? Icon => null;

    public abstract void Bind(Hero hero);
}
