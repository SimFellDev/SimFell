using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 671: "Your offensive abilities have a chance to reset the cooldown of your Heavy Strike and grant you
/// stacks of Kill Em All. While Kill Em All is active, your Heavy Strike has Cooldown Acceleration.
/// Your next Heavy Strike consumes a stack of Kill Em All, causing it to deal more damage." Effect 3160 (`BuffSeconds`,
/// `MaxStacks` stacks). It rolls once per cast of an offensive ability other than Heavy Strike (confirmed in game, 2026-11-01; combat
/// logs, 3 fights with the talent: no Heavy Strike procs it, while the other casts proc it at the expected rate).
/// </summary>
public class KillEmAll : Talent
{
    public const int TalentId = 671;
    public const string TalentName = "Kill Em All";
    public const int EffectId = 3160;
    public const double BuffSeconds = 8.0;
    public const int MaxStacks = 2;
    public const double ProcChance = 0.10;
    /// <summary>Effect 3160: cooldownAcceleration Additive on Heavy Strike. The tooltip's percentage is wrong.</summary>
    public const double CooldownAcceleration = 1.6;
    public const double DamageMultiplier = 2.5;
    public const string ProcKey = "kill_em_all";

    public static readonly AuraDef Buff = AuraDef.Effect("Kill Em All", EffectId, BuffSeconds, MaxStacks) with { IsProc = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeavyStrike>() is not { } heavyStrike) return;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is not (WildSwing or FaceBreaker or SkullCrusher or HammerStorm or CullingStrike or ChainLightning
                or LeapSmash or RagingTempest) || hero.SimContext is not { } ctx) return;
            if (!ctx.Outcomes.RollProc(ProcKey, ProcChance, ctx)) return;
            hero.CooldownOf(heavyStrike).ResetAll();
            hero.Auras.Apply(Buff, now, setStacks: MaxStacks);
        };
        hero.CooldownAccelerations.Add((ability, now) => ability == heavyStrike && hero.Auras.HasAura(Buff, now) ? CooldownAcceleration : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != heavyStrike || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier;
        };
    }
}
