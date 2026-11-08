namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1398: "For a duration you are enraptured in celestial energy, causing your Multishot ability to shoot
/// Empowered Multishots. Can be used during Global Cooldown." Has a cooldown; effect 2009 (a duration and stacks). Fervent
/// Supremacy turns the duration into charges that last longer.
/// </summary>
public partial class SkystridersSupremacy : ElarionAbility
{
    public const int BuffEffectId = 2009;

    public static readonly AuraDef Buff = AuraDef.Effect("Skystrider's Supremacy", BuffEffectId, AbilityDuration, MaxStacks);

    /// <summary>The buff the cast gives (Fervent Supremacy: charges that last longer).</summary>
    public AuraDef CastBuff { get; set; } = Buff;

    /// <summary>Charges the cast gives, each used by one Multishot (Fervent Supremacy); 0 for every Multishot while it lasts.</summary>
    public int ChargesGranted { get; set; }

    public SkystridersSupremacy(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    /// <summary>Takes <paramref name="seconds"/> off the cooldown (Fervent Supremacy).</summary>
    public void ShortenCooldown(double seconds) => Cooldown -= SimTime.FromSeconds(seconds);

    public override void Connect()
    {
        // The first in the list: Skystrider's Supremacy empowers a Multishot before Focused Expanse.
        Elarion.GetAbility<Multishot>()?.EmpoweringBuffs.Insert(0, new Multishot.EmpoweringBuff(
            now => Owner.Auras.HasAura(CastBuff, now),
            now =>
            {
                if (ChargesGranted > 0) Owner.Auras.ConsumeStack(CastBuff, now);
            }));
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(CastBuff, ctx.CurrentTime, setStacks: Math.Max(1, ChargesGranted));
    }
}
