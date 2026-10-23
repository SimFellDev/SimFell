using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 158: "Your Fire Frogs ability conjures 2 additional frogs and all your Fire Frogs now leap on enemies 1
/// additional time. Fire Frogs deal 20% more damage." Their damage over time is a share of their hits, so it gains too.
/// </summary>
public class FrogSquad : Talent
{
    public const int TalentId = 158;
    public const string TalentName = "Frog Squad";
    public const int ExtraFrogs = 2;
    public const int ExtraAttacks = 1;
    public const double DamageMultiplier = 1.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var frogs = hero.GetAbility<FireFrogs>();
        if (frogs == null) return;
        frogs.Frogs += ExtraFrogs;
        frogs.AttacksPerFrog += ExtraAttacks;
        frogs.Attack.AddDamageMultiplier(DamageMultiplier);
    }
}
