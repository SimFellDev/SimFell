namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1623: instantly adds an Engulfing Flames effect on the target, and applies it to other nearby enemies. Enemies
/// without an already active Engulfing Flames and with the highest amount of health have the highest priority. Can be
/// used during Global Cooldown.
/// </summary>
public class Pyromania : ArdeosAbility
{
    public const int AbilityId = 1623;
    public const string AbilityName = "Pyromania";
    public const double CooldownSeconds = 90.0;
    public const int ExtraTargets = 2;
    /// <summary>
    /// The choice of each other enemy, in the order of priority: certain in a sim; a replay takes the enemies the log shows
    /// gaining Engulfing Flames at the cast.
    /// </summary>
    public const string TargetProcKey = "pyromania_target";

    public Pyromania(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    private EngulfingFlamesDot? _dot;

    public override void Connect() => _dot = Ardeos.GetAbility<EngulfingFlames>()?.Dot;

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (_dot == null) return;
        var dots = Ardeos.Dots;
        if (target != null) dots.Apply(ctx, _dot, target);
        // Enemies without the effect first, then the most health first; enemies alike keep their order.
        _others.Clear();
        foreach (var enemy in ctx.ActiveEnemiesExcept(target))
        {
            int at = _others.Count;
            while (at > 0 && Before(dots, enemy, _others[at - 1])) at--;
            _others.Insert(at, enemy);
        }
        int chosen = 0;
        foreach (var enemy in _others)
        {
            if (chosen == ExtraTargets) break;
            if (!ctx.Outcomes.RollProc(TargetProcKey, 1.0, ctx, enemy)) continue;
            dots.Apply(ctx, _dot, enemy);
            chosen++;
        }
    }

    // Speed: the other enemies at the cast in their order of priority, in a list kept between casts.
    private readonly List<EnemyTarget> _others = new();

    private bool Before(DotTracker dots, EnemyTarget a, EnemyTarget b)
    {
        bool aHas = dots.Has(a, _dot!), bHas = dots.Has(b, _dot!);
        return aHas != bHas ? !aHas : a.CurrentHealth.CompareTo(b.CurrentHealth) > 0;
    }
}
