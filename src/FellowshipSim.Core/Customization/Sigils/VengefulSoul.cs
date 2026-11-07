namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 5: "Your critical strikes have a chance to increase your Main Stat for a time.
/// If you are below a health threshold when Vengeful Soul triggers, you are also healed ..." (effect 2163; the heal
/// does not affect damage)
/// </summary>
public class VengefulSoul : Sigil
{
    public const int SigilId = 5;
    public const string SigilName = "Vengeful Soul";
    public const int BuffEffectId = 2163;
    public const double BuffSeconds = 8.0;
    public const double ProcsPerMinute = 3.0;
    public const string ProcKey = "vengeful_soul";
    public static readonly double[] MainStatByRank = [0.03, 0.051, 0.072, 0.093];

    public static readonly AuraDef Buff = AuraDef.Effect("Vengeful Soul", BuffEffectId, BuffSeconds) with { IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Increased(StatType.PrimaryAttributeMultiplier, 1.0 + ForRank(MainStatByRank)));
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.IsCrit && dmg.SimContext is { } ctx && rppm.Roll(hero, ctx)) hero.Auras.Apply(buff, ctx.CurrentTime);
        };
    }
}
