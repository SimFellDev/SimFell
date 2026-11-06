using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 146: each time the damage over time effects of the hero's abilities deal damage, they have a chance to spawn a
/// Fire Frog to leap toward the enemies, and critical strikes have another chance. The frog attacks as a Fire Frogs
/// frog does (Frog Squad's extra attack included).
/// </summary>
public class PyrophibianFrenzy : Talent
{
    public const int TalentId = 146;
    public const string TalentName = "Pyrophibian Frenzy";
    public const double Chance = 0.025;
    public const double CritChance = 0.05;
    public const string ProcKey = "pyrophibian_frenzy";
    /// <summary>
    /// Combat logs (yMAZVJL9hTpFjqbC fight 7, Fc8v6yWxD14GQMnV fight 19; 92 frogs attacking alone): the frog's first attack
    /// comes at <see cref="FirstAttackSeconds"/> after a tick twice as often as at any other delay.
    /// </summary>
    public const double FirstAttackSeconds = 1.25;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var frogs = ardeos.GetAbility<FireFrogs>();
        if (frogs == null) return;
        ardeos.ForEveryDot(dot =>
        {
            dot.Hit += (in DamageContext hit) =>
            {
                if (hit.SimContext is not { } ctx || hit.Amount <= 0) return;
                // The frog leaps at any enemy, so the proof is a frog attack on any of them.
                if (ctx.Outcomes.RollProc(ProcKey, hit.IsCrit ? CritChance : Chance, ctx)) frogs.Spawn(ctx, 1, FirstAttackSeconds);
            };
        });
    }
}
