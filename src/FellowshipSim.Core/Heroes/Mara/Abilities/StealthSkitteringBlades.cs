namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 2309: Skittering Blades used from Stealth. It hits as Skittering Blades does, and applies Volatile Poison
/// (<see cref="VolatilePoison"/>) to every enemy hit.
/// </summary>
public class StealthSkitteringBlades : SkitteringBlades
{
    public new const int AbilityId = 2309;
    public new const string AbilityName = "Stealth Skittering Blades";

    public StealthSkitteringBlades(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        RequiresStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx && hit.Target != null) Mara.VolatilePoison.ApplyTo(ctx, hit.Target);
        };
    }
}
