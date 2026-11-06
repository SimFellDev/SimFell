namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 2308: Backstab used from Stealth. "Backstab - Caustic Poison": instant poison damage, Combo Point generation
/// and a Critical Strike chance bonus on Caustic Poison (Brooding Shadows' text). The Backstab
/// hit itself still lands; the poison is logged as effect 1585 (<see cref="CausticPoison"/>).
/// </summary>
public class StealthBackstab : Backstab
{
    public new const int AbilityId = 2308;
    public new const string AbilityName = "Stealth Backstab";
    public const double StealthComboPoints = 6;

    public StealthBackstab(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        RequiresStealth = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        base.Execute(ctx, target);
        Mara.CausticPoison.Strike(ctx, target);
        GenerateComboPoints(ctx, StealthComboPoints, target);
    }
}
