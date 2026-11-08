using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 644: "Your Successful Blocks have a 6% chance to grant you 2 stacks of Punishing Strikes. Punishing Strikes causes
/// your next Measured Strike to deal 100% increased damage and have 100% increased Veteran of War." The buff is effect 3095.
/// A block is a defensive proc: a replay fires it at each gain of the buff in the log. UNVERIFIED: no log has
/// the talent.
/// </summary>
public partial class PunishingStrikes : Talent
{
    public const int BuffEffectId = 3095;
    public const string ProcKey = "punishing_strikes";

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, maxStacks: MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena) return;
        hero.AddDefensiveProc(ProcKey, (ctx, _) => hero.Auras.Apply(Buff, ctx.CurrentTime, setStacks: StacksAtProc));
        bool empowered = false;
        hero.OnCastResolving += cast =>
        {
            empowered = cast.Ability is MeasuredStrike && hero.Auras.HasAura(Buff, cast.Time);
            if (!empowered) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier;
        };
        helena.VeteranOfWarMultipliers.Add((_, source) => source is MeasuredStrike && empowered ? CooldownReducementMultiplier : 1.0);
    }
}
