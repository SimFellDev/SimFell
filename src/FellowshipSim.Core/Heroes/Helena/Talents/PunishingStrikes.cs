using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 644: "Your Successful Blocks have a 6% chance to grant you 2 stacks of Punishing Strikes. Punishing Strikes causes
/// your next Measured Strike to deal 100% increased damage and have 100% increased Veteran of War." The buff is effect 3095.
/// The engine has no attacks on Helena, so nothing blocks and nothing procs it: a Measured Strike uses up a stack applied
/// some other way. UNVERIFIED: no log has the talent.
/// </summary>
public class PunishingStrikes : Talent
{
    public const int TalentId = 644;
    public const string TalentName = "Punishing Strikes";
    public const int BuffEffectId = 3095;
    public const int StacksPerProc = 2;
    public const double BlockChance = 0.06;
    public const double MeasuredStrikeDamage = 2.0;
    public const double VeteranOfWarMultiplier = 2.0;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, maxStacks: StacksPerProc);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena) return;
        bool empowered = false;
        hero.OnCastResolving += cast =>
        {
            empowered = cast.Ability is MeasuredStrike && hero.Auras.HasAura(Buff, cast.Time);
            if (!empowered) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.DamageMultiplier *= MeasuredStrikeDamage;
        };
        helena.VeteranOfWarMultipliers.Add((_, source) => source is MeasuredStrike && empowered ? VeteranOfWarMultiplier : 1.0);
    }
}
