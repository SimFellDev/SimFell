using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 193: Frost Bolt damage triggers a single Bursting Ice pulse, and Bursting Ice deals more damage.
/// </summary>
public class Burstbolter : Talent
{
    public const int TalentId = 193;
    public const string TalentName = "Burstbolter";
    /// <summary>Bursting Ice damage multiplier.</summary>
    public const double BurstingIceDamage = 1.3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var burstingIce = hero.GetAbility<BurstingIce>();
        burstingIce?.AddDamageMultiplier(BurstingIceDamage);
        // "When your Frost Bolt deals damage to an enemy, it triggers a single pulse of Bursting Ice." (on the bolt's hit,
        // after its travel: yMAZVJL9hTpFjqbC fight 7 @ 57.569 cast, 57.906 hit and pulse)
        var frostBolt = hero.GetAbility<FrostBolt>();
        if (frostBolt != null && burstingIce != null)
        {
            frostBolt.Landed += (ctx, target, dealt) =>
            {
                if (dealt > 0.0) burstingIce.DealPulse(ctx, target);
            };
        }
    }
}
