using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// What proves in a combat log that a proc happened, by the proc's name in the engine (<c>ProcKey</c>). A replay answers
/// each proc roll from the log: it procced if this aura or hit appears right after the roll. A proc not listed here is
/// treated as not procced and reported, so add procs here when adding them to the engine.
/// </summary>
public static class ProcEvidence
{
    /// <summary>
    /// <paramref name="LogId"/> is the aura the proc applies or the hit it deals; it may appear up to
    /// <paramref name="DelaySeconds"/> after the roll (e.g. a projectile's travel), and not before
    /// <paramref name="MinDelaySeconds"/>. With <paramref name="StackJump"/>, the proof is instead that debuff on the
    /// proc's enemy gaining that many stacks at once. With <paramref name="FollowingHits"/>, the proc deals that many more
    /// hits with the ID, every <paramref name="FollowingSpacingSeconds"/> after the first: they belong to that proc and are
    /// no proof of another.
    /// </summary>
    public sealed record Evidence(string ProcKey, int LogId, double DelaySeconds = 0.0, double MinDelaySeconds = 0.0, int StackJump = 0,
        int FollowingHits = 0, double FollowingSpacingSeconds = 0.0);

    public static readonly Evidence[] All =
    [
        new(Hero.SpiritRefundProc, Hero.RisingSpirit.LogId),
        new(UndyingHeart.ProcKey, UndyingHeart.Buff.LogId),
        // The spike erupts from the ground a moment after the proc (logs: about 0.5s).
        new(EmeraldJudgement.ProcKey, LogIds.Effect(EmeraldJudgement.DamageEffectId), DelaySeconds: 1.0),
        new(DragonboneDice.ProcKey, DragonboneDice.Buff.LogId),
        new(VengefulSoul.ProcKey, VengefulSoul.Buff.LogId),
        new(HiddenPower.ProcKey, HiddenPower.Stack.LogId),
        new(InspiredAllegiance.ProcKey, InspiredAllegiance.Buff.LogId),
        // Damage over time: the first tick comes one (hasted) period after the proc.
        new(Kindling.ProcKey, LogIds.Effect(Kindling.DamageEffectId), DelaySeconds: 1.6),
        new(DiamondStrike.ProcKey, LogIds.Effect(DiamondStrike.DamageEffectId), DelaySeconds: 1.0),
        new(RubyStorm.ProcKey, LogIds.Effect(RubyStorm.DamageEffectId), DelaySeconds: 1.0),

        // Gunde
        // A Blood Feather picked up is a stack of Owed in Blood (a replay follows the log's stacks).
        new(Gunde.FeatherProcKey, OwedInBlood.Buff.LogId),
        // Bleeding Hearts' extra strike lands 0.23-0.30s after the cast.
        new(BleedingHearts.ProcKey, HeartSplitter.AbilityId, DelaySeconds: 0.4, MinDelaySeconds: 0.15),
        new(DeathsArc.ProcKey, DeathsArc.Buff.LogId),
        new(GrimHarvest.ProcKey, GrimHarvest.Buff.LogId),
        new(AncestralInstinct.ProcKey, AncestralInstinct.Buff.LogId),

        // Rime
        new(BurstingSwallows.ProcKey, LogIds.Effect(BurstingIce.PulseEffectId)),
        // Avalanche rolls as a comet strikes; its comets strike 5 and 10 server frames later.
        new(Avalanche.ProcKey, IceComet.DamageId, DelaySeconds: 0.25, MinDelaySeconds: 0.1),
        new(Avalanche.SecondProcKey, IceComet.DamageId, DelaySeconds: 0.42, MinDelaySeconds: 0.1),
        // Cold Shower's comet falls first (0.30s + 1.0s/(1 + haste)).
        new(ColdShower.ProcKey, IceComet.DamageId, DelaySeconds: 1.35, MinDelaySeconds: 0.5),
        new(FrostweaversWrath.ProcKey, FrostweaversWrath.Buff.LogId),
        new(SoulfrostTorrent.ProcKey, SoulfrostTorrent.Buff.LogId),
        // A Freezing Torrent crit adding 2 stacks of Coalescing Frost at once instead of 1.
        new(CoalescingFrost.DoubleStackProc, LogIds.Effect(CoalescingFrost.DebuffEffectId), StackJump: 2),

        // Ardeos
        // Pyrophibian Frenzy's frog makes its first attack 1.2-1.3s after the tick that spawned it, then 3 more (Frog
        // Squad, in every logged build with the talent) 40 server frames apart.
        new(PyrophibianFrenzy.ProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 1.35, MinDelaySeconds: 1.15,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        // A Fire Frogs cast's frogs make their first attacks 1.7-2.3s after it: their attacks are no proof of the talent's.
        new(FireFrogs.ConjuredProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 2.4, MinDelaySeconds: 1.5,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        new(ReignOfFire.ProcKey, ReignOfFire.Buff.LogId),
    ];

    private static readonly Dictionary<string, Evidence> ByKey = All.ToDictionary(e => e.ProcKey);

    public static Evidence? For(string procKey) => ByKey.GetValueOrDefault(procKey);
}
