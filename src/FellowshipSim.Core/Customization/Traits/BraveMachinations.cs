namespace FellowshipSim.Core.Customization.Traits;

/// <summary>
/// Trait 48: "Your Weapon Ability's Critical Strike chance is increased by 9.5% / 17% / 24% / 32%. If your Weapon Ability
/// deals a Critical Strike, its Cooldown is reduced by 15% / 20% / 25% / 30%." (of its default cooldown, once per cast).
/// UNVERIFIED: the cooldown part is not yet checked against a log.
/// </summary>
public class BraveMachinations : Trait
{
    public const int TraitId = 48;
    public const string TraitName = "Brave Machinations";
    public static readonly double[] CritChanceByRank = [0.095, 0.17, 0.24, 0.32];
    public static readonly double[] CooldownReductionByRank = [0.15, 0.20, 0.25, 0.30];

    public override int Id => TraitId;
    public override string Name => TraitName;

    public override void Bind(Hero hero)
    {
        if (hero.WeaponAbility is not { } weapon) return;
        weapon.CritChanceBonus += ForRank(CritChanceByRank);
        double reduction = ForRank(CooldownReductionByRank);
        bool reducedThisCast = false;
        // The fight the flag belongs to (each sim iteration and replay has its own).
        SimulationContext? fight = null;
        hero.OnCastCompleted += (ability, _, _) =>
        {
            if (ability == weapon) reducedThisCast = false;
        };
        weapon.Hit += (in hit) =>
        {
            if (fight != hit.SimContext)
            {
                fight = hit.SimContext;
                reducedThisCast = false;
            }
            if (!hit.IsCrit || reducedThisCast) return;
            reducedThisCast = true;
            hero.ReduceCooldown(weapon.Name, SimTime.FromSeconds(reduction * weapon.Cooldown.TotalSeconds), hit.Timestamp);
        };
    }
}
