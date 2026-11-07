namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1310: "Instantly apply stacks of Lunarlight Mark to target enemy and additional enemies near your
/// target. Ability damage you deal to enemies with Lunarlight Mark has a chance to trigger Lunarlight Salvo ...
/// Critical Strikes have a chance to trigger Lunarlight Salvo. Can be used during Global Cooldown." Has a cooldown.
/// The debuff is effect 1876; see <see cref="LunarlightSalvo"/>.
/// </summary>
public class LunarlightMark : ElarionAbility
{
    public const int AbilityId = 1310;
    public const string AbilityName = "Lunarlight Mark";
    public const double CooldownSeconds = 40.0;
    public const int Stacks = 3;
    public const int ExtraTargets = 11;
    public const int DebuffEffectId = 1876;
    public const double DebuffSeconds = 15.0;
    public const int DebuffMaxStacks = 20;

    /// <summary>
    /// The choice of each other enemy: certain in a sim; a replay takes the enemies the log shows gaining Lunarlight Mark
    /// at the cast.
    /// </summary>
    public const string TargetProcKey = "lunarlight_mark_target";

    /// <summary>
    /// Combat logs (fXyzwGcQkTrLD34R fight 4, pWmcV42K39GHTLQX fight 23): every application adds its stacks to the ones
    /// held and restarts the duration (a Spirit Refund adds stacks to a mark already held).
    /// </summary>
    public static readonly AuraDef Debuff = AuraDef.Effect("Lunarlight Mark", DebuffEffectId, DebuffSeconds, DebuffMaxStacks);

    /// <summary>The cast resolved (Resurgent Winds).</summary>
    public event Action<SimulationContext>? Cast;

    public LunarlightMark(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    /// <summary>Adds <paramref name="stacks"/> stacks of Lunarlight Mark to <paramref name="enemy"/>.</summary>
    public static void Mark(SimulationContext ctx, EnemyTarget enemy, int stacks) =>
        enemy.Auras.Apply(Debuff, ctx.CurrentTime, stacks: stacks);

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        if (target != null) Mark(ctx, target, Stacks);
        int chosen = 0;
        foreach (var enemy in ctx.ActiveEnemiesExcept(target))
        {
            if (chosen == ExtraTargets) break;
            if (!ctx.Outcomes.RollProc(TargetProcKey, 1.0, ctx, enemy)) continue;
            Mark(ctx, enemy, Stacks);
            chosen++;
        }
        Cast?.Invoke(ctx);
    }
}
