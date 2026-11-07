namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 2310 ("Lashing Stormkick"): combat logs record each Perfect Storm proc as a cast of it, at the Lashing Stormkick
/// that procced it (7Zmfx9gq6HjvThzc fight 2 @ 25.481). The talent deals its hits.
/// </summary>
public class PerfectStormStrike : MeikoAbility
{
    public const int AbilityId = 2310;
    public const string AbilityName = "Perfect Storm";

    public PerfectStormStrike(Meiko owner) : base(owner, AbilityId, AbilityName)
    {
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
