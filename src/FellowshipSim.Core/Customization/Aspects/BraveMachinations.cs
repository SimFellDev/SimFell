namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 48: "Your Weapon Ability's Critical Strike chance is increased by 9.5% / 17% / 24% / 32%. If your Weapon Ability
/// deals a Critical Strike, its Cooldown is reduced by 15% / 20% / 25% / 30%." (of its default cooldown, once per cast).
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
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability == weapon) armed = true;
        };
        weapon.Hit += (in hit) =>
        {
            if (!hit.IsCrit || !armed) return;
            armed = false;
            hero.ReduceCooldown(weapon.Name, SimTime.FromSeconds(reduction * weapon.Cooldown.TotalSeconds), hit.Timestamp);
        };
    }
}
