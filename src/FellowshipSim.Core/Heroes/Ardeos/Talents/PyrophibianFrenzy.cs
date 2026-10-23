using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 146: "Each time the Damage Over Time effects from your Hero Abilities deal damage, you have a 2.5% chance to
/// spawn a Fire Frog to leap toward your enemies. Critical Strikes have a 5% chance." The frog attacks as a Fire Frogs
/// frog does (Frog Squad's extra attack included).
/// </summary>
public class PyrophibianFrenzy : Talent
{
    public const int TalentId = 146;
    public const string TalentName = "Pyrophibian Frenzy";
    public const double Chance = 0.025;
    public const double CritChance = 0.05;
    public const string ProcKey = "pyrophibian_frenzy";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var frogs = ardeos.GetAbility<FireFrogs>();
        if (frogs == null) return;
        foreach (var dot in ardeos.AllDots)
        {
            dot.Hit += (in DamageContext hit) =>
            {
                if (hit.SimContext is not { } ctx || hit.Amount <= 0) return;
                // The frog leaps at any enemy, so the proof is a frog attack on any of them.
                if (ctx.Outcomes.RollProc(ProcKey, hit.IsCrit ? CritChance : Chance, ctx)) frogs.Spawn(ctx, 1);
            };
        }
    }
}
