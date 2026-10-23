using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 424 (Tariq's Stormlord Helm, item 5899; Drape of the Slayer King, item 5218): "Enemies that take
/// damage from your Leap Smash ability take 20% more damage from you for 6 seconds." Effect 2250 on the enemy.
/// </summary>
public class SlayersMosh : Legendary
{
    public const int PowerId = 424;
    public const int HelmItemId = 5899;
    public const int CloakItemId = 5218;
    public const int DebuffEffectId = 2250;
    public const double DebuffSeconds = 6.0;
    public const double DamageTaken = 1.2;

    public static readonly AuraDef Debuff = AuraDef.Effect("Slayer's Mosh", DebuffEffectId, DebuffSeconds);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Slayer's Mosh";
    public override string HeroName => "Tariq";
    public override IReadOnlyList<int> ItemIds => [HelmItemId, CloakItemId];

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<LeapSmash>() is not { } leapSmash) return;
        leapSmash.Smashed += (ctx, struck) =>
        {
            for (int i = 0; i < struck.Count; i++) struck[i].Auras.Apply(Debuff, ctx.CurrentTime);
        };
        hero.TargetDamageMultipliers.Add((target, now) => target.Auras.HasAura(Debuff, now) ? DamageTaken : 1.0);
    }
}
