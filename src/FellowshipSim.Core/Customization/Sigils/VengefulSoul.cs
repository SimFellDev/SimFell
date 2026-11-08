namespace FellowshipSim.Core.Customization.Sigils;

/// <summary>
/// Sigil 5: "Your critical strikes have a chance to increase your Main Stat for a time.
/// If you are below a health threshold when Vengeful Soul triggers, you are also healed ..." (effect 2163; the heal
/// does not affect damage)
/// </summary>
public partial class VengefulSoul : Sigil
{
    public const int BuffEffectId = 2163;
    public const double BuffSeconds = 8.0;
    public const string ProcKey = "vengeful_soul";

    public static readonly AuraDef Buff = AuraDef.Effect("Vengeful Soul", BuffEffectId, BuffSeconds) with { IsProc = true };

    public override int Id => SigilId;
    public override string Name => SigilName;

    public override void Bind(Hero hero)
    {
        var buff = Buff.WithModifiers(StatModifier.Increased(StatType.PrimaryAttributeMultiplier, ForRank(BuffAdditionalPrimaryStatByRank)));
        var rppm = new RealPpm(ProcKey, SigilProcsPerMinute);
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.IsCrit && dmg.SimContext is { } ctx && rppm.Roll(hero, ctx)) hero.Auras.Apply(buff, ctx.CurrentTime);
        };
    }
}
