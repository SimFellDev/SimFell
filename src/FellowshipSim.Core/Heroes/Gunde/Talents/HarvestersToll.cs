using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 767 / effect 3218: "Your Rupture ability grants you Harvester's Toll, increasing all damage you deal for a
/// time." The effect's own text gives a different value from the talent's; the talent's is used. Combat logs
/// (Y4wpb2f3gc8LqJDX fight 25 @ 26.616): it comes with the Rupture before its hit.
/// </summary>
public class HarvestersToll : Talent
{
    public const int TalentId = 767;
    public const string TalentName = "Harvester's Toll";
    public const int EffectId = 3218;
    public const double BuffSeconds = 10.0;
    public const double DamageMultiplier = 1.075;

    public static readonly AuraDef Buff = AuraDef.Effect("Harvesters Toll", EffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, DamageMultiplier));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability is Rupture) hero.Auras.Apply(Buff, now);
        };
    }
}
