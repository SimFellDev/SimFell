using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 754: "Each spin of Grim Carve reduces the cooldown of your Reaver's Edge, Blood Arc, Heart Splitter and Rupture
/// per spin. Grim Carve deals more damage."
/// </summary>
public class Carnage : Talent
{
    public const int TalentId = 754;
    public const string TalentName = "Carnage";
    public const double DamageMultiplier = 1.25;
    public const double ReductionPerSpinSeconds = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var grimCarve = hero.GetAbility<GrimCarve>();
        if (grimCarve == null) return;
        grimCarve.AddDamageMultiplier(DamageMultiplier);
        Ability?[] reduced = [hero.GetAbility<ReaversEdge>(), hero.GetAbility<BloodArc>(), hero.GetAbility<HeartSplitter>(), hero.GetAbility<Rupture>()];
        var amount = SimTime.FromSeconds(ReductionPerSpinSeconds);
        grimCarve.Pulsed += ctx =>
        {
            foreach (var ability in reduced)
            {
                if (ability != null) hero.ReduceCooldown(ability.Name, amount, ctx.CurrentTime);
            }
        };
    }
}
