namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1398: "For the next 4 seconds you are enraptured in celestial energy, causing your Multishot ability to shoot
/// Empowered Multishots. Can be used during Global Cooldown." Cooldown 45s; effect 2009 (4s, up to 4 stacks). Fervent
/// Supremacy turns the 4 seconds into 4 charges for up to 15 seconds.
/// </summary>
public class SkystridersSupremacy : ElarionAbility
{
    public const int AbilityId = 1398;
    public const string AbilityName = "Skystrider's Supremacy";
    public const double CooldownSeconds = 45.0;
    public const int BuffEffectId = 2009;
    public const double BuffSeconds = 4.0;
    public const int MaxBuffStacks = 4;

    public static readonly AuraDef Buff = AuraDef.Effect("Skystrider's Supremacy", BuffEffectId, BuffSeconds, MaxBuffStacks);

    /// <summary>The buff the cast gives (Fervent Supremacy: 4 charges for 15 seconds).</summary>
    public AuraDef CastBuff { get; set; } = Buff;

    /// <summary>Charges the cast gives, each used by one Multishot (Fervent Supremacy); 0 for every Multishot while it lasts.</summary>
    public int ChargesGranted { get; set; }

    public SkystridersSupremacy(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
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
