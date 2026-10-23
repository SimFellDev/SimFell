namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1623: 90s. "Instantly adds an Engulfing Flames effect on the target, and apply it to up to 2 other nearby
/// enemies. Enemies without an already active Engulfing Flames and with the highest amount of health will be highest
/// priority. Can be used during Global Cooldown."
/// </summary>
public class Pyromania : ArdeosAbility
{
    public const int AbilityId = 1623;
    public const string AbilityName = "Pyromania";
    public const double CooldownSeconds = 90.0;
    public const int ExtraTargets = 2;

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
        var others = ctx.ActiveEnemiesExcept(target)
            .OrderBy(e => dots.Has(e, _dot))
            .ThenByDescending(e => e.CurrentHealth)
            .ToList();
        foreach (var enemy in ctx.Outcomes.SelectTargets(_dot.DamageLogId, others, ctx).Take(ExtraTargets)) dots.Apply(ctx, _dot, enemy);
    }
}
