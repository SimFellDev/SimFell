using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 671: "Your offensive abilities have a 10% chance to reset the cooldown of your Heavy Strike and grant you 2
/// stacks of Kill Em All for 8 seconds. While Kill Em All is active, your Heavy Strike has +60% Cooldown Acceleration.
/// Your next Heavy Strike consumes 1 stack of Kill Em All, causing it to deal 150% more damage." Effect 3160 (8s, 2
/// stacks). It rolls once per cast of an offensive ability other than Heavy Strike (confirmed in game, 2026-11-01; combat
/// logs, 3 fights with the talent: none of 537 Heavy Strikes procs it, where 10% would be about 54, while the other casts
/// proc it 144 times).
/// </summary>
public class KillEmAll : Talent
{
    public const int TalentId = 671;
    public const string TalentName = "Kill Em All";
    public const int EffectId = 3160;
    public const double BuffSeconds = 8.0;
    public const int MaxStacks = 2;
    public const double ProcChance = 0.10;
    public const double CooldownAcceleration = 0.60;
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
        hero.CooldownAccelerationTargetedSources.Add((ability, now) => ability == heavyStrike && hero.Auras.HasAura(Buff, now) ? CooldownAcceleration : 0.0);
        hero.CooldownAccelerationAuraKeys.Add(Buff.Key);
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != heavyStrike || !hero.Auras.HasAura(Buff, cast.Time)) return;
            hero.Auras.ConsumeStack(Buff, cast.Time);
            cast.DamageMultiplier *= DamageMultiplier;
        };
    }
}
