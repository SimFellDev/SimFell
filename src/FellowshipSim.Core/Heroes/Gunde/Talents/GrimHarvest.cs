using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 766 / effect 3206: "Your Reaver's Edge, Blood Arc, Heart Splitter, and Rupture have a chance to reset
/// the cooldown of Grim Carve and grant you Grim Harvest for a time, causing your next Grim Carve to have increased
/// Critical Strike chance." Rolled once per cast. Crit chance beyond a guaranteed crit is a grievous crit.
/// </summary>
public partial class GrimHarvest : Talent
{
    public const int EffectId = 3206;
    public const string ProcKey = "grim_harvest";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, TalentDuration) with { IsProc = true };

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
            return Constants.AddedCritChance;
        });
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (ReaversEdge or BloodArc or HeartSplitter or Rupture) || hero.SimContext is not { } ctx) return;
            if (!ctx.Outcomes.RollProc(ProcKey, Constants.ProcChance, ctx)) return;
            hero.CooldownOf(grimCarve).ResetAll();
            hero.Auras.Apply(Buff, now);
        };
    }
}
