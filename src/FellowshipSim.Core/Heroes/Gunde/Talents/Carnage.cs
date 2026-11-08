using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 754: "Each spin of Grim Carve reduces the cooldown of your Reaver's Edge, Blood Arc, Heart Splitter and Rupture
/// per spin. Grim Carve deals more damage."
/// </summary>
public partial class Carnage : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var grimCarve = hero.GetAbility<GrimCarve>();
        if (grimCarve == null) return;
        grimCarve.AddDamageMultiplier(DamageMultiplier);
        Ability?[] reduced = [hero.GetAbility<ReaversEdge>(), hero.GetAbility<BloodArc>(), hero.GetAbility<HeartSplitter>(), hero.GetAbility<Rupture>()];
        var amount = SimTime.FromSeconds(CooldownReductionInSecondsPerHit);
        grimCarve.Pulsed += ctx =>
        {
            foreach (var ability in reduced)
            {
                if (ability != null) hero.ReduceCooldown(ability.Name, amount, ctx.CurrentTime);
            }
        };
    }
}
