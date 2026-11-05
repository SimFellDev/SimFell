using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 756 / effect 3225 (6s): "Your Reaver's Edge and Grim Carve have a 40% chance to grant you Ancestral Instinct,
/// granting you 6% Strength for 6 seconds." Rolled once per cast. UNVERIFIED: no combat log has this talent.
/// </summary>
public class AncestralInstinct : Talent
{
    public const int TalentId = 756;
    public const string TalentName = "Ancestral Instinct";
    public const int EffectId = 3225;
    public const double BuffSeconds = 6.0;
    public const double ProcChance = 0.4;
    public const double StrengthBonus = 0.06;
    public const string ProcKey = "ancestral_instinct";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, EffectId, BuffSeconds,
        modifiers: StatModifier.Add(StatType.PrimaryAttributeMultiplier, StrengthBonus)) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (ReaversEdge or GrimCarve) || hero.SimContext is not { } ctx) return;
            if (ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) hero.Auras.Apply(Buff, now);
        };
    }
}
