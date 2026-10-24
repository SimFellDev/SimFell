using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 158: "Your Fire Frogs ability conjures 2 additional frogs and all your Fire Frogs now leap on enemies 1
/// additional time. Fire Frogs deal 20% more damage." The 20% applies to the damage over time as well as to the hits it is
/// a share of. In the three logged fights with the talent (yMAZVJL9hTpFjqbC fight 7, Fc8v6yWxD14GQMnV fight 19,
/// YC861NTRgLbmw4Qa fight 31), the replay's effect deals 0.97-1.00 of the logged damage that way, and 0.81-0.83 with the 20%
/// on the hits alone.
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
        frogs.Attack.DotFraction *= DamageMultiplier;
    }
}
