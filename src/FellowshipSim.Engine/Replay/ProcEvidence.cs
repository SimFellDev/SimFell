using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.RelicPassives;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

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
    /// hits with the ID, every <paramref name="FollowingSpacingSeconds"/> after the first (each up to
    /// <paramref name="FollowingSlackSeconds"/> off, for spacing the log rounds to whole server frames): they belong to that
    /// proc and are no proof of another. With <paramref name="Resource"/>, the proof is instead that resource jumping by
    /// <paramref name="ResourceGain"/> between two of the log's readings around the roll, beyond what
    /// <paramref name="ResourceRegenPerSecond"/> regenerates between them. With <paramref name="HitTypes"/>, only a hit of
    /// one of those log hit types is proof (e.g. 22, a grievous crit, for a proc that takes crit chance past 100%). With
    /// <paramref name="StackLossLogId"/>, a hit is proof only when that debuff on the hit's enemy loses a stack in the
    /// same server frame (<paramref name="StackLoss"/> true) or does not (false). With <paramref name="ReservesHit"/>, the
    /// proc deals that hit itself on its enemy alone: the engine's next hit with the ID on the enemy matches the proof, and
    /// hits on other enemies in the same server frame are proof of other procs. With
    /// <paramref name="NotCausedByAbilityId"/>, an aura gain the log names that ability as the cause of is no proof (it is
    /// that ability's own grant). With <paramref name="EnemyDebuffApplied"/>, the proof is instead that debuff of the
    /// player's applied or refreshed on the proc's enemy. With <paramref name="AnyEnemy"/>, a hit on any enemy is proof,
    /// not only on the proc's. With <paramref name="QuietBeforeSeconds"/>, a hit is proof only when no other hit with the ID
    /// landed that long before it (the first hit of a chain, not a bounce of another).
    /// </summary>
    public sealed record Evidence(string ProcKey, int LogId, double DelaySeconds = 0.0, double MinDelaySeconds = 0.0, int StackJump = 0,
        int FollowingHits = 0, double FollowingSpacingSeconds = 0.0, double FollowingSlackSeconds = 0.0, ResourceDef? Resource = null, double ResourceGain = 0.0,
        double ResourceRegenPerSecond = 0.0, IReadOnlyList<int>? HitTypes = null, int StackLossLogId = 0, bool StackLoss = true,
        bool ReservesHit = false, int NotCausedByAbilityId = 0, bool EnemyDebuffApplied = false, bool AnyEnemy = false, double QuietBeforeSeconds = 0.0);

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
        // A proc applies or refreshes the burn on its enemy, after the triggering effect's visual delay (Rime's logs: 78 of
        // 242 applications in the millisecond of a hit on the enemy, 141 within 0.4s of one).
        new(Kindling.ProcKey, LogIds.Effect(Kindling.DamageEffectId), DelaySeconds: 0.45, MinDelaySeconds: -PeriodicEffect.ServerFrameSeconds,
            EnemyDebuffApplied: true),
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
        // Cold Shower's comet strikes 10 server frames after the tick that rolled it (logged within a frame of that).
        new(ColdShower.ProcKey, IceComet.DamageId, DelaySeconds: ColdShower.ImpactDelaySeconds + PeriodicEffect.ServerFrameSeconds,
            MinDelaySeconds: ColdShower.ImpactDelaySeconds - PeriodicEffect.ServerFrameSeconds),
        new(FrostweaversWrath.ProcKey, FrostweaversWrath.Buff.LogId),
        new(SoulfrostTorrent.ProcKey, SoulfrostTorrent.Buff.LogId),
        // A Freezing Torrent crit adding 2 stacks of Coalescing Frost at once instead of 1.
        new(CoalescingFrost.DoubleStackProc, LogIds.Effect(CoalescingFrost.DebuffEffectId), StackJump: 2),

        // Ardeos
        // Pyrophibian Frenzy's frog makes its first attack 1.2-1.3s after the tick that spawned it, then 3 more (Frog
        // Squad, in every logged build with the talent) 20 server frames apart.
        new(PyrophibianFrenzy.ProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 1.35, MinDelaySeconds: 1.15,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        // A Fire Frogs cast's frogs make their first attacks 1.7-2.6s after it: their attacks are no proof of the talent's.
        new(FireFrogs.ConjuredProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 2.8, MinDelaySeconds: 1.5,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        // An enemy Pyromania chose gains Engulfing Flames at the cast.
        new(Pyromania.TargetProcKey, LogIds.Effect(EngulfingFlamesDot.EffectId), StackJump: 1),
        new(ReignOfFire.ProcKey, ReignOfFire.Buff.LogId),
        // Spontaneous Combustion takes the tick past 100% crit chance: the tick is a grievous crit, within 0.35s of the
        // engine's tick (the engine's ticks drift from the log's as haste changes).
        new(SpontaneousCombustion.SearingBlazeProcKey, LogIds.Effect(SearingBlazeDot.EffectId), DelaySeconds: 0.35, MinDelaySeconds: -0.35,
            HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),
        new(SpontaneousCombustion.EngulfingFlamesProcKey, LogIds.Effect(EngulfingFlamesDot.EffectId), DelaySeconds: 0.35, MinDelaySeconds: -0.35,
            HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),

        // Tariq
        new(Schism.SkullCrusherProcKey, Schism.SkullCrusherBuff.LogId),
        new(Schism.HammerStormProcKey, Schism.HammerStormBuff.LogId),
        new(KillEmAll.ProcKey, KillEmAll.Buff.LogId),
        new(ExecutionersGrin.ProcKey, ExecutionersGrin.Buff.LogId),
        // Them Bones takes Skull Crusher past 100% crit chance: its hits are grievous crits. Nothing else gives it grievous crits.
        new(ThemBones.ProcKey, SkullCrusher.AbilityId, DelaySeconds: 0.1, HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),
        // A Wild Swing the enemy avoids is a miss or a dodge, neither dealing damage.
        new(WildSwing.AvoidedProcKey, WildSwing.AbilityId, DelaySeconds: 0.05, HitTypes: [ReplayScriptBuilder.HitTypeMiss, ReplayScriptBuilder.HitTypeDodge]),
        // A free Chain Lightning's first hit is logged 0.50s after the proc (the engine's 0.30s after a cast). Its bounces
        // (every 0.05s, logged 1 or 2 server frames apart, so a chain's first hit has none 0.1s before it) are no proof of
        // another. Ace of Spades' chain starts on the enemy the proccing hit struck; Blood & Thunder's mostly elsewhere (26
        // of 34 not on Heavy Strike's target; q3y7pWgXrjN2dftw fight 12).
        new(BloodAndThunder.ProcKey, LogIds.Effect(ChainLightning.DamageEffectId),
            DelaySeconds: 0.55, MinDelaySeconds: 0.45,
            FollowingHits: ChainLightning.Hits + Thunderstruck.ExtraBounces - 1, FollowingSpacingSeconds: ChainLightning.BounceSeconds,
            FollowingSlackSeconds: PeriodicEffect.ServerFrameSeconds / 2, AnyEnemy: true, QuietBeforeSeconds: 0.1),
        new(AceOfSpades.ProcKey, LogIds.Effect(ChainLightning.DamageEffectId),
            DelaySeconds: 0.55, MinDelaySeconds: 0.45,
            FollowingHits: ChainLightning.Hits + Thunderstruck.ExtraBounces - 1, FollowingSpacingSeconds: ChainLightning.BounceSeconds,
            FollowingSlackSeconds: PeriodicEffect.ServerFrameSeconds / 2, QuietBeforeSeconds: 0.1),

        // Mara
        // 10 Energy at once (Fc8v6yWxD14GQMnV fight 19: 172 jumps of 8-12 Energy right after a poison hit, regeneration aside).
        new(VenomousDelight.ProcKey, 0, Resource: Mara.Energy, ResourceGain: VenomousDelight.EnergyGained, ResourceRegenPerSecond: 25),
        new(CausticWounds.ProcKey, LogIds.Effect(CausticPoison.EffectId)),
        new(Hemotoxin.ProcKey, LogIds.Effect(Hemotoxin.PoisonEffectId), StackJump: 1),
        // The spill's first tick comes 1.5s (hasted) after the proc.
        new(CorrosiveSpill.ProcKey, LogIds.Effect(CorrosiveSpill.DamageEffectId), DelaySeconds: 1.6, MinDelaySeconds: 0.8),
        new(SeethingBurst.ProcKey, LogIds.Effect(SeethingBurst.DamageEffectId)),
        new(FromTheShadows.ProcKey, QueensFang.AbilityId, DelaySeconds: 0.5),

        // Elarion
        // The salvo is logged within 2ms of the hit that triggered it; the mark loses a stack with it, but for a salvo
        // a Starfall Volley arrow triggered with Lunarlight Affinity.
        new(LunarlightSalvo.ProcKey, LogIds.Effect(LunarlightSalvo.DamageEffectId), StackLossLogId: LunarlightMark.Debuff.LogId,
            ReservesHit: true),
        new(LunarlightSalvo.KeptMarkProcKey, LogIds.Effect(LunarlightSalvo.DamageEffectId), StackLossLogId: LunarlightMark.Debuff.LogId,
            StackLoss: false, ReservesHit: true),
        new(LunarlightSalvo.EruptProcKey, LogIds.Effect(LunarlightSalvo.EruptEffectId), DelaySeconds: 0.1, MinDelaySeconds: -0.01),
        // An enemy Lunarlight Mark or a Spirit Refund chose gains stacks of Lunarlight Mark at once.
        new(LunarlightMark.TargetProcKey, LunarlightMark.Debuff.LogId, StackJump: 1),
        new(Elarion.RefundMarkTargetProcKey, LunarlightMark.Debuff.LogId, StackJump: 1),
        new(CelestialShot.ImpetusProcKey, CelestialShot.Impetus.LogId),
        new(FocusedExpanse.ProcKey, FocusedExpanse.Buff.LogId),
        // Lunarlight Mark's own grant (the log names 1310 as its cause) is no proof of the 5% proc.
        new(ResurgentWinds.ProcKey, ResurgentWinds.Buff.LogId, NotCausedByAbilityId: LunarlightMark.AbilityId),
        new(StarstrikersAscent.ProcKey, ImpendingHeartseeker.Buff.LogId),
        // A Starfall Volley arrow on the enemy: the first free one landing up to 1.5s after the release.
        new(StarfallVolley.ArrowProcKey, StarfallVolley.AbilityId, DelaySeconds: 1.5, MinDelaySeconds: -0.15, ReservesHit: true),
        // Lethal Shots takes Highwind Arrow past 100% crit chance: its first hit, 0.13s after the cast, is a grievous crit.
        new(LethalShots.ProcKey, HighwindArrow.AbilityId, DelaySeconds: 0.5, HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),
    ];

    private static readonly Dictionary<string, Evidence> ByKey = All.ToDictionary(e => e.ProcKey);

    public static Evidence? For(string procKey) => ByKey.GetValueOrDefault(procKey);
}
