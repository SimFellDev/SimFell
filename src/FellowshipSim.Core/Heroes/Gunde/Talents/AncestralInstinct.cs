using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 756 / effect 3225: "Your Reaver's Edge and Grim Carve have a chance to grant you Ancestral Instinct,
/// granting you Strength for a time." Rolled once per cast. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class AncestralInstinct : Talent
{
    public const int EffectId = 3225;
    public const string ProcKey = "ancestral_instinct";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, TalentDuration,
        modifiers: StatModifier.Add(StatType.PrimaryAttributeMultiplier, Constants.StrengthMultiplier - 1.0)) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (ReaversEdge or GrimCarve) || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, Constants.ProcChance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
