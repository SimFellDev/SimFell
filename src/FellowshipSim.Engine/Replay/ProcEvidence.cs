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
using FellowshipSim.Core.Heroes.Helena.Abilities;
using FellowshipSim.Core.Heroes.Helena.Talents;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Heroes.Meiko.Talents;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;
using FellowshipSim.Core.Heroes.Xavian;
using FellowshipSim.Core.Heroes.Xavian.Abilities;
using FellowshipSim.Core.Heroes.Xavian.Talents;

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
    /// <paramref name="ResourceRegen"/> gives per second now and what <paramref name="ResourceSpent"/> gives for a cast
    /// reading's ability, up to <paramref name="ResourceGainsAtOnce"/> gains in one jump. With <paramref name="HitTypes"/>, only a hit of
    /// one of those log hit types is proof (e.g. 22, a grievous crit, for a proc that takes crit chance past full). With
    /// <paramref name="StackLossLogId"/>, a hit is proof only when that debuff on the hit's enemy loses a stack in the
    /// same server frame (<paramref name="StackLoss"/> true) or does not (false). With <paramref name="ReservesHit"/>, the
    /// proc deals that hit itself on its enemy alone: the engine's next hit with the ID on the enemy matches the proof, and
    /// hits on other enemies in the same server frame are proof of other procs. With
    /// <paramref name="NotCausedByAbilityId"/>, an aura gain the log names that ability as the cause of is no proof (it is
    /// that ability's own grant). With <paramref name="EnemyDebuffApplied"/>, the proof is instead that debuff of the
    /// player's applied or refreshed on the proc's enemy. With <paramref name="AnyEnemy"/>, a hit on any enemy is proof,
    /// not only on the proc's. With <paramref name="QuietBeforeSeconds"/>, a hit is proof only when no other hit with the ID
    /// landed that long before it (the first hit of a chain, not a bounce of another). With <paramref name="EachHitProves"/>,
    /// each hit with the ID is the proof of one proc: hits in one server frame are as many procs, not one group.
    /// </summary>
    public sealed record Evidence(string ProcKey, int LogId, double DelaySeconds = 0.0, double MinDelaySeconds = 0.0, int StackJump = 0,
        int FollowingHits = 0, double FollowingSpacingSeconds = 0.0, double FollowingSlackSeconds = 0.0, ResourceDef? Resource = null, double ResourceGain = 0.0,
        Func<Hero, SimTime, double>? ResourceRegen = null, Func<Hero, int, double>? ResourceSpent = null, int ResourceGainsAtOnce = 1,
        IReadOnlyList<int>? HitTypes = null, int StackLossLogId = 0, bool StackLoss = true,
        bool ReservesHit = false, int NotCausedByAbilityId = 0, bool EnemyDebuffApplied = false, bool AnyEnemy = false, double QuietBeforeSeconds = 0.0,
        bool EachHitProves = false);

    public static readonly Evidence[] All =
    [
        new(Hero.SpiritRefundProc, Hero.RisingSpirit.LogId),
        new(UndyingHeart.ProcKey, UndyingHeart.Buff.LogId),
        // The spike lands in the graph run of its roll, with no delay. In the 23 Mara fights of
        // data/fights, nearly every spike is logged in the row before a hit on their enemy. An engine hit lands up to a frame
        // from its log hit (T2qLPpmMkrAtJ9B1 fight 18: Seething Poison in the engine, spike and log hit; 3M2vWr8taxVfTQFY
        // fight 53: spike, Queen's Fang): with no frame either side, fewer spikes match than with one. A periodic tick lands
        // over a frame from its log hit: on the 78 fights of Rime, Ardeos, Tariq, Gunde and Elarion, two frames either side
        // match more spikes than one.
        new(EmeraldJudgement.ProcKey, LogIds.Effect(EmeraldJudgement.DamageEffectId),
            DelaySeconds: 2 * PeriodicEffect.ServerFrameSeconds, MinDelaySeconds: -2 * PeriodicEffect.ServerFrameSeconds),
        new(DragonboneDice.ProcKey, DragonboneDice.Buff.LogId),
        new(VengefulSoul.ProcKey, VengefulSoul.Buff.LogId),
        new(HiddenPower.ProcKey, HiddenPower.Stack.LogId),
        new(InspiredAllegiance.ProcKey, InspiredAllegiance.Buff.LogId),
        // Damage over time: the first tick comes one (hasted) period after the proc.
        // A proc applies or refreshes the burn on its enemy, after the triggering effect's visual delay (Rime's logs: many
        // applications come in the millisecond of a hit on the enemy, and more soon after one).
        new(Kindling.ProcKey, LogIds.Effect(Kindling.DamageEffectId), DelaySeconds: 0.45, MinDelaySeconds: -PeriodicEffect.ServerFrameSeconds,
            EnemyDebuffApplied: true),
        new(DiamondStrike.ProcKey, LogIds.Effect(DiamondStrike.DamageEffectId), DelaySeconds: 1.0),
        new(RubyStorm.ProcKey, LogIds.Effect(RubyStorm.DamageEffectId), DelaySeconds: 1.0),

        // Gunde
        // A Blood Feather picked up is a stack of Owed in Blood (a replay follows the log's stacks).
        new(Gunde.FeatherProcKey, OwedInBlood.Buff.LogId),
        // Bleeding Hearts' extra strike lands shortly after the cast.
        new(BleedingHearts.ProcKey, HeartSplitter.AbilityId, DelaySeconds: 0.4, MinDelaySeconds: 0.15),
        new(DeathsArc.ProcKey, DeathsArc.Buff.LogId),
        new(GrimHarvest.ProcKey, GrimHarvest.Buff.LogId),
        new(AncestralInstinct.ProcKey, AncestralInstinct.Buff.LogId),

        // Rime
        new(BurstingSwallows.ProcKey, LogIds.Effect(BurstingIce.PulseEffectId)),
        // Avalanche rolls as a comet strikes; its comets strike some server frames later.
        new(Avalanche.ProcKey, IceComet.DamageId, DelaySeconds: 0.25, MinDelaySeconds: 0.1),
        new(Avalanche.SecondProcKey, IceComet.DamageId, DelaySeconds: 0.42, MinDelaySeconds: 0.1),
        // Cold Shower's comet strikes `ColdShower.ImpactDelaySeconds` after the tick that rolled it (logged within a frame of that).
        new(ColdShower.ProcKey, IceComet.DamageId, DelaySeconds: ColdShower.ImpactDelaySeconds + PeriodicEffect.ServerFrameSeconds,
            MinDelaySeconds: ColdShower.ImpactDelaySeconds - PeriodicEffect.ServerFrameSeconds),
        new(FrostweaversWrath.ProcKey, FrostweaversWrath.Buff.LogId),
        new(SoulfrostTorrent.ProcKey, SoulfrostTorrent.Buff.LogId),
        // A Freezing Torrent crit adding extra stacks of Coalescing Frost at once.
        new(CoalescingFrost.DoubleStackProc, LogIds.Effect(CoalescingFrost.DebuffEffectId), StackJump: 2),

        // Ardeos
        // Pyrophibian Frenzy's frog makes its first attack after the tick that spawned it, then more (Frog Squad, in every
        // logged build with the talent) a fixed number of server frames apart.
        new(PyrophibianFrenzy.ProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 1.35, MinDelaySeconds: 1.15,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        // A Fire Frogs cast's frogs make their first attacks later than that: their attacks are no proof of the talent's.
        new(FireFrogs.ConjuredProcKey, LogIds.Effect(FireFrogAttack.DamageEffectId), DelaySeconds: 2.8, MinDelaySeconds: 1.5,
            FollowingHits: FireFrogs.BaseAttacksPerFrog + FrogSquad.ExtraAttacks - 1, FollowingSpacingSeconds: FireFrogs.AttackIntervalSeconds),
        // An enemy Pyromania chose gains Engulfing Flames at the cast.
        new(Pyromania.TargetProcKey, LogIds.Effect(EngulfingFlamesDot.EffectId), StackJump: 1),
        new(ReignOfFire.ProcKey, ReignOfFire.Buff.LogId),
        // Spontaneous Combustion takes the tick past full crit chance: the tick is a grievous crit, close to the
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
        // Them Bones takes Skull Crusher past full crit chance: its hits are grievous crits. Nothing else gives it grievous crits.
        new(ThemBones.ProcKey, SkullCrusher.AbilityId, DelaySeconds: 0.1, HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),
        // A Wild Swing the enemy avoids is a miss or a dodge, neither dealing damage.
        new(WildSwing.AvoidedProcKey, WildSwing.AbilityId, DelaySeconds: 0.05, HitTypes: [ReplayScriptBuilder.HitTypeMiss, ReplayScriptBuilder.HitTypeDodge]),
        // A free Chain Lightning's first hit is logged later after the proc than after a cast in the engine. Its bounces
        // (logged a server frame or two apart, so a chain's first hit has none shortly before it) are no proof of
        // another. Ace of Spades' chain starts on the enemy the proccing hit struck; Blood & Thunder's mostly elsewhere
        // (not on Heavy Strike's target; q3y7pWgXrjN2dftw fight 12).
        new(BloodAndThunder.ProcKey, LogIds.Effect(ChainLightning.DamageEffectId),
            DelaySeconds: 0.55, MinDelaySeconds: 0.45,
            FollowingHits: ChainLightning.Constants.MaxNumOfTargets + Thunderstruck.ExtraBounces - 1, FollowingSpacingSeconds: ChainLightning.Constants.TimeBetweenTargets,
            FollowingSlackSeconds: PeriodicEffect.ServerFrameSeconds / 2, AnyEnemy: true, QuietBeforeSeconds: 0.1),
        new(AceOfSpades.ProcKey, LogIds.Effect(ChainLightning.DamageEffectId),
            DelaySeconds: 0.55, MinDelaySeconds: 0.45,
            FollowingHits: ChainLightning.Constants.MaxNumOfTargets + Thunderstruck.ExtraBounces - 1, FollowingSpacingSeconds: ChainLightning.Constants.TimeBetweenTargets,
            FollowingSlackSeconds: PeriodicEffect.ServerFrameSeconds / 2, QuietBeforeSeconds: 0.1),

        // Meiko
        // Perfect Storm's extra kick is 0.40s after the Lashing Stormkick that procced it, on every enemy the kick struck.
        new(PerfectStorm.ProcKey, PerfectStorm.DamageId, DelaySeconds: 0.45, MinDelaySeconds: 0.35),
        // A Shattering Obelisk pulse is its hit, on every enemy nearby. UNVERIFIED: no combat log has the talent.
        new(ShatteringObelisk.ProcKey, ShatteringObelisk.PulseLogId),

        // Mara
        // `VenomousDelight.EnergyAdded` Energy at once. Between neighbouring readings in the 23 Mara fights of
        // data/fights, regeneration and a cast's cost aside, jumps of one gain occur with the talent and not without it,
        // jumps of two gains are rare and jumps of three rarer, so one jump is proof of two procs at most.
        new(VenomousDelight.ProcKey, 0, Resource: Mara.Energy, ResourceGain: VenomousDelight.Constants.EnergyAdded,
            ResourceRegen: (hero, now) => ((Mara)hero).EnergyRate(now), ResourceSpent: (hero, abilityId) => ((Mara)hero).EnergySpentBy(abilityId),
            ResourceGainsAtOnce: 2),
        new(CausticWounds.ProcKey, LogIds.Effect(CausticPoison.EffectId)),
        new(Hemotoxin.ProcKey, LogIds.Effect(Hemotoxin.PoisonEffectId), StackJump: 1),
        // The spill's first tick comes one (hasted) period after the proc.
        new(CorrosiveSpill.ProcKey, LogIds.Effect(CorrosiveSpill.DamageEffectId), DelaySeconds: 1.6, MinDelaySeconds: 0.8),
        new(SeethingBurst.ProcKey, LogIds.Effect(SeethingBurst.DamageEffectId)),
        new(FromTheShadows.ProcKey, QueensFang.AbilityId, DelaySeconds: 0.5),

        // Elarion
        // The salvo is logged within milliseconds of the hit that triggered it; the mark loses a stack with it, but for a salvo
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
        // Lunarlight Mark's own grant (the log names 1310 as its cause) is no proof of the Resurgent Winds proc.
        new(ResurgentWinds.ProcKey, ResurgentWinds.Buff.LogId, NotCausedByAbilityId: LunarlightMark.AbilityId),
        new(StarstrikersAscent.ProcKey, ImpendingHeartseeker.Buff.LogId),
        // A Starfall Volley arrow on the enemy: the first free one landing after the release.
        new(StarfallVolley.ArrowProcKey, StarfallVolley.AbilityId, DelaySeconds: 1.5, MinDelaySeconds: -0.15, ReservesHit: true),
        // Lethal Shots takes Highwind Arrow past full crit chance: its first hit, shortly after the cast, is a grievous crit.
        new(LethalShots.ProcKey, HighwindArrow.AbilityId, DelaySeconds: 0.5, HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),

        // Xavian
        // A Swift Reprieval stack comes with the hit that rolled it (XyGpcHjNaAzgk7mW fight 20 @ 17.238).
        new(SunStrike.ProcKey, Xavian.SwiftReprieval.LogId),
        new(BlindingSlash.ProcKey, Xavian.SwiftReprieval.LogId),
        new(Omnistrike.ProcKey, Xavian.SwiftReprieval.LogId),
        new(GoldenHour.ProcKey, GoldenHour.Buff.LogId),
        new(SunsVerdict.ProcKey, SunsVerdict.Buff.LogId),
        // Solar Glare takes Solar Blades past 100% crit chance: its hits are grievous crits.
        new(SolarGlare.ProcKey, SolarBlades.AbilityId, DelaySeconds: 0.1, HitTypes: [ReplayScriptBuilder.HitTypeGrievousCrit]),
        // A swing the enemy avoids is a miss or a dodge, neither dealing damage.
        new(Core.Heroes.Xavian.Abilities.Attack.AvoidedProcKey, Core.Heroes.Xavian.Abilities.Attack.AbilityId, DelaySeconds: 0.05,
            HitTypes: [ReplayScriptBuilder.HitTypeMiss, ReplayScriptBuilder.HitTypeDodge]),
        // A Parry's Counter-Attack, Hallowed Shield stack and Vanguard of Vengeance stack (defensive procs).
        // Each Counter-Attack hit is its own Parry: two Parries in one server frame are two hits.
        new(VanguardOfVengeance.CounterAttackProcKey, LogIds.Effect(VanguardOfVengeance.CounterAttackEffectId), EachHitProves: true),
        new(VanguardOfVengeance.StackProcKey, VanguardOfVengeance.Buff.LogId),
        new(HallowedShield.ProcKey, HallowedShield.Buff.LogId),
        // Solaris: the hits of one overheal (a defensive proc).
        new(Xavian.SolarisProcKey, Xavian.SolarisLogId),
        // Aura of Solace: the hits of one pulse (a logged pulse).
        new(AuraOfSolace.PulseProcKey, LogIds.Effect(AuraOfSolace.DamageEffectId)),

        // Helena
        // Each bounce of a Shield Throw is a hit on an enemy the shield has not hit yet, landing 0.03-0.2s after the hit
        // before it (HrNZ6wQfFKGxybDR fight 1 @ 16.027). A hit lands no earlier than the engine releases it, and the first
        // hit is released up to 0.1s after the log's, so a bounce may be logged up to 0.1s before the engine's hit lands
        // (1p7AXmHfwV9CYMxR fight 30 @ 80.282).
        new(ShieldThrow.BounceProcKey, ShieldThrow.AbilityId, DelaySeconds: 0.4, MinDelaySeconds: -0.1, ReservesHit: true),
        // A Shield Throw's first hit, released 0.3s after the cast: a log hit of the shield from 0.15s before to 0.3s after the
        // release (0.27-0.37s after the cast in HrNZ6wQfFKGxybDR fight 1 and v8rhNxVc3PwDqzBA fight 4),
        // with no hit of the shield in the 0.2s before it (a throw's bounces are 0.03-0.2s apart). Over the 18 fights the
        // window of -0.1 to 0.1s leaves 1,194 log hits of the shield unproduced, this one 592.
        new(ShieldThrow.FirstHitProcKey, ShieldThrow.AbilityId, DelaySeconds: 0.3, MinDelaySeconds: -0.15, ReservesHit: true, QuietBeforeSeconds: 0.2),
        // A swing the enemy avoids is a miss or a dodge, neither dealing damage.
        new(FellowshipSim.Core.Heroes.Helena.Abilities.Attack.AvoidedProcKey, FellowshipSim.Core.Heroes.Helena.Abilities.Attack.AbilityId,
            DelaySeconds: 0.05, HitTypes: [ReplayScriptBuilder.HitTypeMiss, ReplayScriptBuilder.HitTypeDodge]),
        // Defensive procs (blocks and damage taken): each gain of the buff in the log, and each reflect hit.
        new(TheBestDefense.ProcKey, TheBestDefense.Buff.LogId),
        new(PunishingStrikes.ProcKey, PunishingStrikes.Buff.LogId),
        new(SecondWind.ProcKey, SecondWind.Buff.LogId),
        new(UnscarredSoul.ProcKey, LogIds.Effect(UnscarredSoul.ReflectEffectId), ReservesHit: true),
        // Hold the Line's own Empowered Shield Slam (the log names 975 as its cause) is no proof of Sword & Board's.
        new(SwordAndBoard.ProcKey, HoldTheLine.Empowered.LogId, NotCausedByAbilityId: HoldTheLine.AbilityId),
    ];

    private static readonly Dictionary<string, Evidence> ByKey = All.ToDictionary(e => e.ProcKey);

    public static Evidence? For(string procKey) => ByKey.GetValueOrDefault(procKey);

    /// <summary>
    /// Readings at most this far apart. Combat logs (23 Mara fights of <c>data/fights</c>): the rises above regeneration with
    /// Venomous Delight are nearly all inside it, and none occur with no talent.
    /// </summary>
    public const double ResourceEvidenceWindowSeconds = 1.5;

    /// <summary>
    /// Every proof of <paramref name="evidence"/> in the log, of the kind <see cref="LogOutcomeSource.RollProc"/> reads it
    /// as: its time and, for a proof on an enemy, that enemy. A resource rise is at its first reading. A hit proof is the
    /// first hit of each group of hits in one server frame, with the hits that follow it; with <c>EachHitProves</c>, every hit.
    /// </summary>
    public static IEnumerable<(double Time, UnitRef? Target)> Times(ReplayScript script, Evidence evidence, Hero hero)
    {
        int player = script.Player.Id;
        if (evidence.Resource != null)
        {
            foreach (var rise in ResourceRises(script.Observations.Resources.OrderBy(r => r.Time).ToList(), evidence, hero))
            {
                double regenPerSecond = evidence.ResourceRegen?.Invoke(hero, SimTime.FromSeconds(rise.From)) ?? 0.0;
                if (ResourceGains(rise, evidence, regenPerSecond) > 0) yield return (rise.From, null);
            }
            yield break;
        }
        var debuffs = script.Observations.Auras.Where(a => a.AbilityId == evidence.LogId && a.Source.Id == player && a.Target.Id != player);
        if (evidence.EnemyDebuffApplied)
        {
            foreach (var a in debuffs.Where(a => a.Type is "applydebuff" or "refreshdebuff").OrderBy(a => a.Time)) yield return (a.Time, a.Target);
            yield break;
        }
        if (evidence.StackJump > 0)
        {
            // Gains in one millisecond on one enemy add up (a new debuff given several stacks at once).
            foreach (var group in debuffs.GroupBy(a => a.Target))
            {
                foreach (var gains in StackChanges(group).Gains.GroupBy(g => g.Event.Time))
                {
                    if (gains.Sum(g => g.Added) >= evidence.StackJump) yield return (gains.Key, group.Key);
                }
            }
            yield break;
        }
        var auras = script.Observations.Auras.Where(a => a.AbilityId == evidence.LogId && a.Target.Id == player && a.Source.Id == player).ToList();
        if (auras.Count > 0)
        {
            foreach (var gain in auras.Where(a => a.IsGain && (evidence.NotCausedByAbilityId == 0 || a.ExtraAbilityId != evidence.NotCausedByAbilityId))
                         .OrderBy(a => a.Time))
            {
                yield return (gain.Time, null);
            }
            yield break;
        }
        double groupEnd = double.NegativeInfinity;
        foreach (var hit in script.Observations.Damage.Where(d => d.AbilityId == evidence.LogId).OrderBy(d => d.Time))
        {
            if (hit.Time <= groupEnd && !evidence.EachHitProves) continue;
            groupEnd = hit.Time + PeriodicEffect.ServerFrameSeconds
                       + evidence.FollowingHits * (1.5 * evidence.FollowingSpacingSeconds + evidence.FollowingSlackSeconds);
            yield return (hit.Time, hit.Target);
        }
    }

    /// <summary>
    /// The rises of <paramref name="evidence"/>'s resource between neighbouring readings at most
    /// <see cref="ResourceEvidenceWindowSeconds"/> apart that can hold a gain. A reading at the resource's maximum starts none.
    /// </summary>
    public static List<ResourceRise> ResourceRises(IReadOnlyList<LogResourceSnapshot> readings, Evidence evidence, Hero hero)
    {
        var rises = new List<ResourceRise>();
        var log = LogResources.All.FirstOrDefault(r => r.Resource == evidence.Resource);
        if (log == null) return rises;
        string type = log.LogResourceType.ToString();
        double max = evidence.Resource!.Max;
        LogResourceSnapshot? previous = null;
        double previousAmount = 0;
        foreach (var reading in readings)
        {
            if (!reading.Resources.TryGetValue(type, out var value)) continue;
            double amount = log.EngineAmount(value);
            if (previous != null && reading.Time - previous.Time <= ResourceEvidenceWindowSeconds && previousAmount < max - 0.5)
            {
                bool fromCast = previous.Event == "cast" && previous.Activation;
                double spent = fromCast && previous.AbilityId is { } abilityId ? evidence.ResourceSpent?.Invoke(hero, abilityId) ?? 0.0 : 0.0;
                double rise = amount - previousAmount + spent;
                if (rise >= 0.7 * evidence.ResourceGain) rises.Add(new ResourceRise(previous.Time, reading.Time, rise, fromCast, amount >= max - 0.5));
            }
            previous = reading;
            previousAmount = amount;
        }
        return rises;
    }

    /// <summary>
    /// The gains of <paramref name="evidence"/> in <paramref name="rise"/> beyond <paramref name="regenPerSecond"/>, within a
    /// slack: 0 for a rise that fits no number of gains up to <c>ResourceGainsAtOnce</c>, and at most 1 for a rise to the maximum.
    /// </summary>
    public static int ResourceGains(ResourceRise rise, Evidence evidence, double regenPerSecond)
    {
        double excess = rise.Amount - regenPerSecond * (rise.To - rise.From);
        double slack = 0.3 * evidence.ResourceGain;
        if (rise.ToMaximum) return excess >= evidence.ResourceGain - slack ? 1 : 0;
        int gains = (int)Math.Round(excess / evidence.ResourceGain);
        return gains <= evidence.ResourceGainsAtOnce && Math.Abs(excess - gains * evidence.ResourceGain) <= slack ? Math.Max(gains, 0) : 0;
    }

    /// <summary>One rise of a resource between two of the log's readings, a reading at a cast counting its cost back.</summary>
    public readonly record struct ResourceRise(double From, double To, double Amount, bool FromCast, bool ToMaximum);

    /// <summary>
    /// One debuff's events on one enemy: each gain with the stacks it added, and the times it lost stacks, in time order.
    /// </summary>
    public static (List<(LogAura Event, int Added)> Gains, List<double> Losses) StackChanges(IEnumerable<LogAura> events)
    {
        int stacks = 0;
        var gains = new List<(LogAura Event, int Added)>();
        var losses = new List<double>();
        foreach (var a in events.OrderBy(a => a.Time))
        {
            int now = a.Type is "removedebuff" or "removebuff" ? 0 : a.Stack ?? (a.IsGain ? Math.Max(stacks, 1) : stacks);
            if (a.IsGain && now > stacks) gains.Add((a, now - stacks));
            if (a.IsLoss && now < stacks) losses.Add(a.Time);
            stacks = now;
        }
        return (gains, losses);
    }
}
