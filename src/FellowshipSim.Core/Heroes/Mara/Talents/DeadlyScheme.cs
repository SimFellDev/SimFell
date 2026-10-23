using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 110: "For every 5 Energy you spend you gain 1 stack of Deadly Scheme (effect 2434). At 40 stacks it activates
/// for 12 seconds (effect 2068), causing your next Queen's Fang or Arachnid Assault to have +100% Critical Strike chance."
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 176 activations): the stacks past 40 carry over; while it is active the
/// stacks keep counting without the buff, and come back as the finisher uses it up (@ 25.692: 37 stacks, a 40 Energy
/// Queen's Fang activates it with 5 over; a 45 Energy Arachnid Assault uses it up with 14 stacks).
/// </summary>
public class DeadlyScheme : Talent
{
    public const int TalentId = 110;
    public const string TalentName = "Deadly Scheme";
    public const int StackEffectId = 2434;
    public const int ActiveEffectId = 2068;
    public const double EnergyPerStack = 5;
    public const int StacksToActivate = 40;
    public const double ActiveSeconds = 12.0;
    public const double CritChance = 1.0;

    public static readonly AuraDef Stacks = AuraDef.Effect(TalentName, StackEffectId, maxStacks: StacksToActivate);
    public static readonly AuraDef Active = AuraDef.Effect("Deadly Scheme: Active", ActiveEffectId, ActiveSeconds);

    public override string Name => TalentName;
    public override int Id => TalentId;

    private double _energy;
    private SimulationContext? _fight;

    public override void Bind(Hero hero)
    {
        hero.OnResourceSpent += (resource, amount, now) =>
        {
            if (resource != Mara.Energy || amount <= 0 || hero.SimContext is not { } ctx) return;
            if (_fight != ctx)
            {
                _energy = 0;
                _fight = ctx;
            }
            _energy += amount;
            if (hero.Auras.HasAura(Active, now)) return;
            Show(hero, now);
        };
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not (QueensFang or ArachnidAssault) || !hero.Auras.HasAura(Active, cast.Time)) return;
            hero.Auras.Remove(Active, cast.Time);
            cast.CritChance += CritChance;
            Show(hero, cast.Time);
        };
    }

    /// <summary>Shows the stacks counted; at 40 they activate Deadly Scheme and the rest carry over.</summary>
    private void Show(Hero hero, SimTime now)
    {
        int stacks = (int)(_energy / EnergyPerStack);
        if (stacks >= StacksToActivate)
        {
            _energy -= StacksToActivate * EnergyPerStack;
            hero.Auras.Remove(Stacks, now);
            hero.Auras.Apply(Active, now);
            return;
        }
        if (stacks > 0) hero.Auras.Apply(Stacks, now, setStacks: stacks);
    }
}
