namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 1: "Your abilities have a chance to grant you a stack of Hidden Power for a time. At the maximum stacks,
/// Hidden Power is consumed and Power Revealed is granted to you for a time, increasing your Main Stat by an amount
/// per rank. You can not gain Hidden Power stacks while Power Revealed is active on you."
/// (effects 2164 Hidden Power, 2165 Power Revealed)
/// </summary>
public partial class HiddenPower : Sigil
{
    public const int StackEffectId = 2164;
    public const int BuffEffectId = 2165;
    public const double StackSeconds = 60.0;
    public const int StacksNeeded = 5;
    public const double BuffSeconds = 15.0;
    public const string ProcKey = "hidden_power";

    // Combat logs: the stacks and Power Revealed stay through the hero's death (6 deaths with stacks, 4 with Power
    // Revealed, none removed them; Fc8v6yWxD14GQMnV fight 19 @ 473.014).
    public static readonly AuraDef Stack = AuraDef.Effect("Hidden Power", StackEffectId, StackSeconds, StacksNeeded) with { KeepsOnDeath = true, IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var revealed = hero.Register(AuraDef.Effect("Power Revealed", BuffEffectId, BuffSeconds,
            modifiers: StatModifier.Increased(StatType.PrimaryAttributeMultiplier, ForRank(Constants.BuffAdditionalMainStatByRank))) with { KeepsOnDeath = true, IsProc = true });
        var rppm = new RealPpm(ProcKey, SigilProcsPerMinute);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is not { } ctx || hero.Auras.HasAura(revealed, now) || !rppm.Roll(hero, ctx)) return;
            if (hero.Auras.Apply(Stack, now).Stacks < StacksNeeded) return;
            hero.Auras.Remove(Stack, now);
            hero.Auras.Apply(revealed, now);
        };
    }
}
