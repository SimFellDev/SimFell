using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Core.Heroes.Ardeos;

/// <summary>
/// Ardeos, the Pyromancer. His abilities generate Cinders; every 100 Cinders are a Burning Ember. Detonate spends them.
/// His fire damage over time effects run on <see cref="Dots"/>. Abilities are in Abilities/, talents in Talents/.
/// </summary>
public sealed class Ardeos : Hero, ITargetEffects
{
    /// <summary>"For every 100 Cinder you generate, you gain 1 Burning Ember, shown in the UI ... as four orbs."</summary>
    public const double CindersPerBurningEmber = 100.0;
    public const int MaxBurningEmbers = 4;

    /// <summary>
    /// hero_data Ardeos MaxResources 400: Cinders, 100 to a Burning Ember. Combat logs report them as resource type 2 in
    /// hundredths (max 40000).
    /// </summary>
    public static readonly ResourceDef Cinders = new("Cinders", Max: CindersPerBurningEmber * MaxBurningEmbers);

    /// <summary>Class tuning: hero attribute BalancingDamageAura (entities.jsonl v26136736, Ardeos baseAttributes), +10%.</summary>
    public const double BalancingDamageAura = 1.1;

    /// <summary>Ardeos's talent tree, as in game (talent-slot records in entities.jsonl v26136736).</summary>
    public static readonly TalentSlot[] TalentTree = TalentSlot.Tree(
        //              row cost  a                            b                              c
        TalentSlot.InRow(1, 2,    SlowBurn.TalentId,           FrogSquad.TalentId,            GreatBallsOfFire.TalentId),
        TalentSlot.InRow(2, 1,    Backdraft.TalentId,          FlareUp.TalentId,              ApocalypticSurge.TalentId),
        TalentSlot.InRow(3, 2,    AgonizingBlaze.TalentId,     Firestarter.TalentId,          UndyingFlame.TalentId),
        TalentSlot.InRow(4, 1,    CascadingInferno.TalentId,   CrashAndBurn.TalentId,         CracklingInferno.TalentId),
        TalentSlot.InRow(5, 3,    RollingFlames.TalentId,      PyrophibianFrenzy.TalentId,    ReignOfFire.TalentId),
        TalentSlot.InRow(6, 1,    BurningInitiative.TalentId,  IntensifyingInferno.TalentId,  SpontaneousCombustion.TalentId));

    public override IReadOnlyList<TalentSlot> TalentTreeSlots => TalentTree;

    /// <summary>Datamine hero 7: mainStat Intellect.</summary>
    public override MainStat MainStat => MainStat.Intellect;

    /// <summary>Every fire damage over time effect of Ardeos's abilities on enemies.</summary>
    public DotTracker Dots { get; }

    /// <summary>Every kind of damage over time effect the hero has (each registers itself).</summary>
    public List<DamageOverTime> AllDots { get; } = new();

    private readonly List<Action<DamageOverTime>> _everyDot = new();

    /// <summary>
    /// Runs <paramref name="action"/> on every kind of damage over time effect: those the hero has now and those a later
    /// talent or legendary adds (Crackling Inferno's and Explosivo's burns), whatever the order they bind in.
    /// </summary>
    public void ForEveryDot(Action<DamageOverTime> action)
    {
        _everyDot.Add(action);
        foreach (var dot in AllDots.ToList()) action(dot);
    }

    /// <summary>A new kind of damage over time effect registers itself.</summary>
    internal void AddDot(DamageOverTime dot)
    {
        AllDots.Add(dot);
        foreach (var action in _everyDot) action(dot);
    }

    public Ardeos() => Dots = new DotTracker(this);

    public IEnumerable<string> TargetEffectNames => AllDots.Select(d => d.Name);

    /// <summary>
    /// A damage over time effect on <paramref name="target"/>, as one: running until its last copy runs out, with the
    /// stacks of every copy (Engulfing Flames runs as copies side by side). <see cref="TargetEffectPick.Oldest"/> and
    /// <see cref="TargetEffectPick.Newest"/> read the copy applied first or last, with its own expiry and stacks.
    /// </summary>
    public TargetEffect? FindOn(EnemyTarget target, string slug, SimTime now, TargetEffectPick pick = TargetEffectPick.All)
    {
        var on = Dots.On(target);
        DotInstance? picked = null;
        var expiresAt = SimTime.Zero;
        int stacks = 0;
        for (int i = 0; i < on.Count; i++)
        {
            var dot = on[i];
            if (!dot.IsActive || dot.ExpiresAt <= now) continue;
            if (dot.Effect.AplSlug != slug) continue;
            stacks += dot.Stacks;
            if (dot.ExpiresAt > expiresAt) expiresAt = dot.ExpiresAt;
            if (picked == null
                || (pick == TargetEffectPick.Oldest && dot.AppliedAt < picked.AppliedAt)
                || (pick == TargetEffectPick.Newest && dot.AppliedAt >= picked.AppliedAt))
            {
                picked = dot;
            }
        }
        if (picked == null) return null;
        var effect = picked.Effect;
        int maxStacks = effect.Stacking == DotStacking.Concurrent ? int.MaxValue : effect.MaxStacks;
        return pick == TargetEffectPick.All
            ? new TargetEffect(expiresAt, stacks, maxStacks, SimTime.FromSeconds(effect.Duration))
            : new TargetEffect(picked.ExpiresAt, picked.Stacks, maxStacks, SimTime.FromSeconds(effect.Duration));
    }

    /// <summary>Whole Burning Embers held.</summary>
    public int BurningEmbers => (int)Math.Floor(ResourceAmount(Cinders) / CindersPerBurningEmber + 1e-9);

    public override void Initialize()
    {
        AddResource(Cinders);
        SpiritRefundResource = Cinders;
        ClassDamageTuning = BalancingDamageAura;

        Abilities.Add(new FireBall(this));
        Abilities.Add(new FireFrogs(this));
        Abilities.Add(new EngulfingFlames(this));
        Abilities.Add(new SearingBlaze(this));
        Abilities.Add(new InfernalWave(this));
        Abilities.Add(new Detonate(this));
        Abilities.Add(new Wildfire(this));
        Abilities.Add(new Pyromania(this));
        Abilities.Add(new Apocalypse(this));
        Abilities.Add(new Incinerate(this));
    }

    /// <summary>Adds Cinders (every 100 are a Burning Ember).</summary>
    public void GainCinders(double amount, SimulationContext? ctx, EnemyTarget? from = null) => GainResource(Cinders, amount, ctx, from);

    public override void ResetForIteration()
    {
        ResetCombatState();
        Dots.Reset();
        foreach (var frogs in Abilities.OfType<FireFrogs>()) frogs.ResetState();
    }
}
