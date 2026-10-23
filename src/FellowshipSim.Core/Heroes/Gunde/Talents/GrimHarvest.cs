using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 766 / effect 3206 (8s): "Your Reaver's Edge, Blood Arc, Heart Splitter, and Rupture have a 6% chance to reset
/// the cooldown of Grim Carve and grant you Grim Harvest for 8 seconds, causing your next Grim Carve to have +100%
/// Critical Strike chance." Rolled once per cast. Overflow beyond 100% is a grievous crit.
/// </summary>
public class GrimHarvest : Talent
{
    public const int TalentId = 766;
    public const string TalentName = "Grim Harvest";
    public const int EffectId = 3206;
    public const double BuffSeconds = 8.0;
    public const double ProcChance = 0.06;
    public const double CritChance = 1.0;
    public const string ProcKey = "grim_harvest";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var grimCarve = hero.GetAbility<GrimCarve>();
        if (grimCarve == null) return;
        grimCarve.CastCritChanceBonuses.Add(ctx =>
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return 0.0;
            hero.Auras.Remove(Buff, ctx.CurrentTime);
            return CritChance;
        });
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (ReaversEdge or BloodArc or HeartSplitter or Rupture) || hero.SimContext is not { } ctx) return;
            if (!ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) return;
            hero.CooldownOf(grimCarve).ResetAll();
            hero.Auras.Apply(Buff, now);
        };
    }
}
