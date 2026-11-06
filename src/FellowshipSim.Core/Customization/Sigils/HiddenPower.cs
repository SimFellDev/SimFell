namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 1: "Your abilities have a chance to grant you 1 stack of Hidden Power for 60 seconds. When you have 5 stacks,
/// Hidden Power is consumed and Power Revealed is granted to you for 15 seconds, increasing your Main Stat by
/// 7.2% / 12.8% / 18.4% / 24%. You can not gain Hidden Power stacks while Power Revealed is active on you. (2.6 PPM)"
/// (effects 2164 Hidden Power, 2165 Power Revealed)
/// </summary>
public class HiddenPower : Sigil
{
    public const int SigilId = 1;
    public const string SigilName = "Hidden Power";
    public const int StackEffectId = 2164;
    public const int BuffEffectId = 2165;
    public const double StackSeconds = 60.0;
    public const int StacksNeeded = 5;
    public const double BuffSeconds = 15.0;
    public const double ProcsPerMinute = 2.6;
    public const string ProcKey = "hidden_power";
    public static readonly double[] MainStatByRank = [0.072, 0.128, 0.184, 0.24];

    // Combat logs: the stacks and Power Revealed stay through the hero's death (6 deaths with stacks, 4 with Power
    // Revealed, none removed them; Fc8v6yWxD14GQMnV fight 19 @ 473.014).
    public static readonly AuraDef Stack = AuraDef.Effect("Hidden Power", StackEffectId, StackSeconds, StacksNeeded) with { KeepsOnDeath = true, IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var revealed = hero.Register(AuraDef.Effect("Power Revealed", BuffEffectId, BuffSeconds,
            modifiers: StatModifier.Increased(StatType.PrimaryAttributeMultiplier, 1.0 + ForRank(MainStatByRank))) with { KeepsOnDeath = true, IsProc = true });
        var rppm = new RealPpm(ProcKey, ProcsPerMinute, PpmScaling.Haste);
        hero.OnCastCompleted += (_, _, now) =>
        {
            if (hero.SimContext is not { } ctx || hero.Auras.HasAura(revealed, now) || !rppm.Roll(hero, ctx)) return;
            if (hero.Auras.Apply(Stack, now).Stacks < StacksNeeded) return;
            hero.Auras.Remove(Stack, now);
            hero.Auras.Apply(revealed, now);
        };
    }
}
