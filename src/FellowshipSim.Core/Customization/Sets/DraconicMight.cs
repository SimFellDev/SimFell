namespace FellowshipSim.Core.Customization.Sets;

/// <summary>
/// Set bonus power 239: "Your critical strikes have a chance (0.9 PPM) to increase your Intellect by 18% for 14 seconds."
/// (effect 1986; the Agility and Strength versions raise their own stat; the game data gives it a 5 second internal
/// cooldown)
/// </summary>
public class DraconicMight : SetBonus
{
    public const string SetName = "Draconic Might";
    public const int AgilitySetBonusId = 677;
    public const int IntellectSetBonusId = 678;
    public const int StrengthSetBonusId = 679;
    public const int SetBonusPowerId = 239;
    public const int BuffEffectId = 1986;
    public const double BuffSeconds = 14.0;
    public const double MainStatMultiplier = 1.18;
    public const double ProcsPerMinute = 0.9;
    public const double InternalCooldownSeconds = 5.0;
    public const string ProcKey = "draconic_might";

    public static readonly AuraDef Buff = AuraDef.Effect(SetName, BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.PrimaryAttributeMultiplier, MainStatMultiplier));

    public override string Name => SetName;
    public override IReadOnlyList<int> SetBonusIds => [AgilitySetBonusId, IntellectSetBonusId, StrengthSetBonusId];

    public override void Bind(Hero hero)
    {
        var rppm = new RealPpm(ProcKey, ProcsPerMinute);
        var ready = SimTime.Zero;
        // The fight the internal cooldown belongs to (each sim iteration and replay has its own).
        SimulationContext? fight = null;
        hero.OnDamageDealt += (in dmg) =>
        {
            if (!dmg.IsCrit || dmg.SimContext is not { } ctx) return;
            if (fight != ctx)
            {
                fight = ctx;
                ready = SimTime.Zero;
            }
            if (ctx.CurrentTime < ready || !rppm.Roll(hero, ctx)) return;
            ready = ctx.CurrentTime + SimTime.FromSeconds(InternalCooldownSeconds);
            hero.Auras.Apply(Buff, ctx.CurrentTime);
        };
    }
}
