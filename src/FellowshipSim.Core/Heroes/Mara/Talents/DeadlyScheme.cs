using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 110: "For every few Energy you spend you gain a stack of Deadly Scheme (effect 2434). At full stacks it activates
/// (effect 2068), causing your next Queen's Fang or Arachnid Assault to have increased Critical Strike chance."
/// Combat logs (Fc8v6yWxD14GQMnV fight 19): the stacks past <see cref="StacksToActivate"/> carry over; while it is active the
/// stacks keep counting without the buff, and come back as the finisher uses it up (@ 25.692).
/// Effect 2434 is off while Active is on (its ongoing tag requirements ignore `CustomGrantedTags.Mara.Talent.ResourceAttackSpenderCritChance.Proc`) and keeps its
/// stack count: the count a spend set, or <see cref="StacksToActivate"/> with no spend during Active. The talent's ability
/// sets the stack count, and no node runs as Active runs out (T2qLPpmMkrAtJ9B1 fight 18
/// @ 544.776: the buff is back at full stacks, and the next spend shows the count).
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
            if (hero.Auras.HasAura(Active, now))
            {
                _spentWhileActive = true;
                return;
            }
            Show(hero, now);
        };
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability is not (QueensFang or ArachnidAssault) || !hero.Auras.HasAura(Active, cast.Time)) return;
            _changing = true;
            hero.Auras.Remove(Active, cast.Time);
            _changing = false;
            cast.CritChance += CritChance;
            Show(hero, cast.Time);
        };
        // The stacks counted go when something else takes the buffs off (combat logs, fight 19 @ 473.015: Mara's death
        // removes them, and the count starts again after she is back). Deadly Scheme: Active running out keeps them.
        hero.Auras.OnRemoved += (aura, at) =>
        {
            if (_changing) return;
            if (aura.Def.Key == Stacks.Key || (aura.Def.Key == Active.Key && at < aura.ExpiresAt)) _energy = 0;
            else if (aura.Def.Key == Active.Key && HeldStacks > 0)
            {
                _changing = true;
                hero.Auras.Apply(Stacks, at, setStacks: HeldStacks);
                _changing = false;
            }
        };
        // The stack count is the count:
        // stacks given from outside set it (combat logs, GXFz3pZ94aQr2chV fight 3 @ 1.036: the first spend shows the
        // stacks held from before the fight plus the spend's).
        hero.Auras.OnGained += (aura, _, _) =>
        {
            if (_changing || aura.Def.Key != Stacks.Key) return;
            _fight = hero.SimContext;
            _energy = aura.Stacks * EnergyPerStack + _energy % EnergyPerStack;
        };
    }

    /// <summary>The talent itself is changing the buffs.</summary>
    private bool _changing;

    /// <summary>A spend came during Deadly Scheme: Active.</summary>
    private bool _spentWhileActive;

    /// <summary>Effect 2434's stack count as Active ends: <see cref="StacksToActivate"/> until a spend sets it to the count.</summary>
    private int HeldStacks => _spentWhileActive ? (int)(_energy / EnergyPerStack) : StacksToActivate;

    /// <summary>Shows the stacks counted; at <see cref="StacksToActivate"/> they activate Deadly Scheme and the rest carry over.</summary>
    private void Show(Hero hero, SimTime now)
    {
        int stacks = (int)(_energy / EnergyPerStack);
        if (stacks >= StacksToActivate)
        {
            _energy -= StacksToActivate * EnergyPerStack;
            _spentWhileActive = false;
            _changing = true;
            hero.Auras.Remove(Stacks, now);
            _changing = false;
            hero.Auras.Apply(Active, now);
            return;
        }
        if (stacks == 0) return;
        _changing = true;
        hero.Auras.Apply(Stacks, now, setStacks: stacks);
        _changing = false;
    }
}
