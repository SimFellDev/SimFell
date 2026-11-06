namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 48: "Your Weapon Ability's Critical Strike chance is increased by a percentage by rank. If your Weapon Ability
/// deals a Critical Strike, its Cooldown is reduced by a percentage by rank." (of its default cooldown, once per cast).
/// UNVERIFIED: the cooldown part is not yet checked against a log.
/// </summary>
public class BraveMachinations : Aspect
{
    public const int AspectId = 48;
    public const string AspectName = "Brave Machinations";
    public static readonly double[] CritChanceByRank = [0.095, 0.17, 0.24, 0.32];
    public static readonly double[] CooldownReductionByRank = [0.15, 0.20, 0.25, 0.30];

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        if (hero.WeaponAbility is not { } weapon) return;
        weapon.CritChanceBonus += ForRank(CritChanceByRank);
        double reduction = ForRank(CooldownReductionByRank);
        // Each completed cast of the weapon ability arms one cooldown reduction, and the first crit takes it. The cast resolving is its
        // completion, before any of its hits (OnCastCompleted comes after an instant weapon ability's hits).
        bool armed = false;
        // The fight the flag belongs to (each sim iteration and replay has its own, and the hero is reused).
        SimulationContext? fight = null;
        hero.OnCastResolving += cast =>
        {
            if (fight != cast.SimContext)
            {
                fight = cast.SimContext;
                armed = false;
            }
            if (cast.Ability == weapon) armed = true;
        };
        weapon.Hit += (in hit) =>
        {
            if (fight != hit.SimContext)
            {
                fight = hit.SimContext;
                armed = false;
            }
            if (!hit.IsCrit || !armed) return;
            armed = false;
            hero.ReduceCooldown(weapon.Name, SimTime.FromSeconds(reduction * weapon.Cooldown.TotalSeconds), hit.Timestamp);
        };
    }
}
