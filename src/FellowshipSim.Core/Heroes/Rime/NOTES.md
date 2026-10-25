# Rime: what the logs showed

How Rime's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
Measured on 10 Rime logs (fights on Wyrmheart and Apostate Veras, July to October 2026) unless noted.

## Damage

- **Class tuning:** +4% damage (hero attribute `BalancingDamageAura` 1.04) since 2026-08-27; replays of fights before
  then run without it (`ClassTuningHistory` in the replay). It applies to the weapon ability too: Chronoshift matches the
  logs with it after that date and without it before.
- **Target falloff** (damage per target beyond T targets is sqrt(T/N)): Bursting Ice 5, Rising Talons 12, Ice Comet 12,
  Glacial Assault's explosion 12, Chronoshift 5, The Vehement 5.
- **Rising Talons:** one wave of spikes per Winter Orb; the first 0.30s after the cast, then every 4 server frames.
  Every wave keeps the damage and target falloff of the cast, but hits the enemies present when it erupts.
- **Glacial Assault's explosion:** 30% of the hit actually dealt (crit included); it never crits itself.
- **Burstbolter:** the pulse comes when the Frost Bolt lands.
- **Coalescing Frost** (10 fights of patch 0.5.0.0, 300 eruptions): the eruption strikes its own enemy for the full
  damage and the enemies around it with sqrt(5/M) beyond 5 of them, M being their number. The erupting enemy's hit over
  the others' is sqrt((N - 1) / 5) with N enemies struck: 1.20 at N = 8, 1.33 at 10, 1.53 at 12, 1.80 at 16, 1.89 at 19,
  and 1.0 up to N = 6. Calibration went from 1.118 / 1.119 (normal / crit) to 1.047 / 1.045.
- **Godfall Wardstone's Vulnerable** (effects 1108 and 1112, IncomingDamageMultiplier 4 Multiplicitive for 8s once the
  Wardstone is stunned or interrupted): Rime's hits on Wardstones were 3.3-4.8 of the engine (GWChJ8P7tn6fHgV3 #20: 79
  hits on 138.1-138.4, 3.4% of the log's damage; AY1m6jPKTZyx4vz2 #9: 18 on 87.1, 1.9%). The replay applies the logged
  debuff: exact replay of the two fights went from 95.3% and 97.2% to 97.5% and 99.2% of the log.
- **Returned Buff** (effect 2463 of Deathless Katrine's Sever Soul, DamageScale 1.4 MultiplyCompound for 15s, against
  the description's 20%): Rime's 70 hits under it in NhBnFXmy3bJg9Cav #33 (inmy) were 1.409 of the engine, and 1.007
  with the logged buff applied. Exact replay of the fight went from 98.7% to 98.8%.
- **Coalescing Frost's double stacks:** a Freezing Torrent crit that starts the debuff (2306) with 2 stacks is logged as
  applydebuff and applydebuffstack 2 in the same millisecond: 220 in the 15 fights of patch 0.5.0.0, each on a crit;
  173 crits start it with 1 and no normal hit with 2. The replay reads the pair as one gain of 2: the engine's stacks at
  GWChJ8P7tn6fHgV3 #22's 45 eruptions on their own enemy went from 631 to 646 (the log's 654), and calibration from
  1.060 / 1.061 to 1.025 / 1.024.
- **Harrowing Ice** (fight g6zBdpvJCmh2HfrD #31): a pulse deals damage with the stacks held before it, then adds its own;
  stacks reach 31 (the tooltip says 30) and are consumed at once.

- **Grievous crits:** crit chance above 100% gives base x (crit multiplier + overflow) (UCRDamageCalculationBase
  0x145105A4A, Global.GrievousCritScalar 1.0). Ice Comet hits at 100%+ crit chance in the engine went from 0.985 to 0.998
  of the engine (585 hits; Wobra's in D1fBhVPkwYKXWbpr #1 0.976 to 0.999), Glacial Blast's from 0.979 to 0.993.
- **Rising Spirit** (effect 3115, +5% Intellect per stack) adds to Mote of Creation's +10% (effect 3085; both
  PrimaryAttributeMultiplier Additive): Intellect under n stacks is (1.1 + 0.05 n) / 1.1 of Intellect without. Matched hits
  under Rising Spirit over hits without it (log/engine, the 15 fights of patch 0.5.0.0 but NhBnFXmy3bJg9Cav #32 and #33),
  the engine multiplying the two before and adding them after: 0.995, 0.992, 0.991, 0.990 and 0.982 at 1 to 5 stacks
  (24,426, 11,905, 4,170, 1,311 and 569 hits) before; 0.999, 1.000, 1.003, 1.005 and 1.000 after.
- **The Vehement** (glyph 712): the hero's death removes the counter (effect 3194), and the next BASIC hit counts 1
  (GWChJ8P7tn6fHgV3 #20 @ 551.968 and 824.381: the counter removed 3ms after each death). A Frost Bolt the log never
  shows landing does not count (4Ln6PQZCRzc7Wpta #77 @ 116.780, #78 @ 1311.371, GWChJ8P7tn6fHgV3 #22 @ 597.179,
  Rzg1nQNHpm2fhYtB #23 @ 981.460): 12 of 1,248 Frost Bolt casts in the 15 fights of patch 0.5.0.0 complete with no hit on
  their living target, and the replay lets them hit nothing. Before both, one miscount put every later eruption a BASIC
  hit early: 463 of 1,808 eruption hits were missing; after them, none.
- **Soulfrost Torrent:** the empowered Torrent's ticks get +100% crit chance once (effect 1391's execution modifier,
  row Rime.ChanneledBeamSingleDamage.BoostByCritProc.CritChance) and deal 1 + the hero's CritChance attribute more
  (GA_Rime_ChanneledBeamSingleDamage ubergraph 2513-2914, build 26162532; the attribute has neither The Sinister's nor
  Sealed Fate's bonus, game-data-binary: GetFloatAttribute reads the tagless stored value). The (1 + CritChance) term is
  a developer bug in the game, not how grievous crits work; the engine models the game's current behaviour in
  Soulfrost Torrent's class alone until the game fixes it. Its ticks per player went from 0.897-1.004 of the engine (the engine's +200%) to 0.994-1.016 (Slick,
  Xaxina, Loreuxe, Zya, sww), Freezing Torrent's crit calibration from 0.979 to 1.007.

## Timing

- **Bursting Ice:** pulses are hasted. The debuff ends 1/60s after its 3s, with a partial final pulse worth the time
  left, only when its period has run at least MinimumPartialTickTime 0.1s (period - remaining; DefaultGame.ini
  `CRAbilitySystemGlobals`, the partial tick 0x1452D0C70; game-data-authority answer 10). The engine read the time since
  the last pulse before, which a re-arm since that pulse makes longer: in Rzg1nQNHpm2fhYtB #24 @ 336.998 the log has no
  pulse at the removal 99ms after the last one, and the engine dealt one 103ms after its own last pulse, its period
  re-armed by the Focused Haste stack of the Ice Comet at 336.964. The mismatches before
  casts of Anima went from 4,305 to 4,269 and Winter Orbs from 1,026 to 1,013, Frost Swallow 1033 engine-only hits from
  1,388 to 1,378 (pass-36 to pass-48).
- **Haste on a damage over time** (Bursting Ice's debuff 1395, Amethyst Splinter 101 and Kindling 104, each tagged
  EffectType.Dot; game-data-binary, build 26162532): each change of Rime's Haste re-arms the period of every running one
  at Period / Haste, the next tick at the new period x the share of the old period left (0x1452EBCA0, 0x1452EE870,
  0x1452D88D0). A change inside a tick's own execution leaves no share (FTimerManager::GetTimerRemaining 0x144041D00
  reads 0 for the executing timer): the tick comes again on the next server frame at full damage, and the ticks after it
  come every new period from the first. Inside a Bursting Ice pulse are its own hits' procs (Adrenaline Rush) and a cast
  the server's ability queue held: the pulse's Anima helper (ability 1033) ends inside the pulse, and every ability end
  flushes the queue (UCRGameplayAbility::EndAbility 0x1450DACD0, 0x1452D7370). The replay runs a cast inside a pulse
  when the log has it in the pulse's server frame after the pulse. In the 5 fights without Bursting Swallows
  (GWChJ8P7tn6fHgV3 #20 and #22, jHDC613ZzbTYR8wW #18, Rzg1nQNHpm2fhYtB #23 and #24) the engine repeats 23 pulses, 22 of
  them among the log's 36 (GWChJ8P7tn6fHgV3 #20: pulse 48.059, Adrenaline Rush 48.061, again 48.092, next 48.458, one
  period of 0.384s after the first rounded up to the frame). Bursting Ice hits matched went from 66,746 to 67,193 of
  68,700, Frost Swallow 1033 engine-only hits from 1,650 to 1,396, and the mismatches before casts of Anima from 5,039 to
  4,606, Winter Orbs from 1,599 to 1,351 and Harrowing Ice's wrong stacks from 1,041 to 926 (pass-29 to pass-31).
- **A haste change before the frame's timers** moves a damage over time's next tick a server frame later: the server
  activates a player's ability from the client's request or its ability queue before FTimerManager::Tick (UWorld::Tick
  0x143B785E0), so GetTimerRemaining reads the timer against the last frame's time and the re-armed timer waits for this
  frame's time (InternalSetTimer 0x144041D40, FTimerManager::Tick 0x144057664). An instant cast right after a cast's
  completion or a channel's end in the same frame comes from the queue that ability's end flushed, inside the frame's
  timers, and moves nothing. In the 5 fights without Bursting Swallows, the log's lag of a pulse behind the engine's
  grew by a median 28ms (mean 21.7ms) across a Focused Haste change from an instant cast (58 pulse pairs), against 1ms
  with no haste change between the pulses (2,035); with the frame in the engine, -4ms (mean -9.8ms). Bursting Ice hits
  matched went from 67,193 to 67,383, Frost Swallow 1033 hits matched from 28,595 to 28,666, and the mismatches before
  casts of Anima from 4,606 to 4,550, Winter Orbs from 1,351 to 1,200 and Harrowing Ice's stacks from 977 to 830
  (pass-31 to pass-33). Amethyst Splinter ticks matched went from 30,991 to 30,957, and Seized Opportunity's mismatches
  from 1,384 to 1,506.
- **Curse of An'zhyr** (effect 2286 of weapon ability 1561: EffectType.Dot without a tickrate exemption, period 3s,
  Infinite, no tick on application; game-data-authority): its ticks run on the period timer each Haste change re-arms,
  as Bursting Ice's do. In sww's fights (Rzg1nQNHpm2fhYtB #23 and #24) the median lag of the log's tick behind the
  engine's went from 212ms to 166ms and the ticks within 50ms from 15.8% to 19.3%. Curse hits matched went from 2,387 to
  2,385 of 2,400 (pass-35 to pass-36): in #23 the engine's tick now comes 89ms after the log's at 1091.940 and no longer
  reaches enemy 23.16, and the one at 585.724 is lost where the engine's haste is below the log's (below).
- **Bursting Ice ends with its enemy** (effect 1395, EffectLogic.RemoveOnDeath): 47 of 785 debuffs end early in the 15
  fights, 46 with no pulse at the removal, and Winter's Embrace ends with them (GWChJ8P7tn6fHgV3 #20 @ 352.564). The
  engine pulsed on to the end: engine-only multi-target Bursting Ice hits went from 384 to 0, Frost Swallow engine-only
  hits from 1,975 to 1,697 and Anima mismatches before casts from 5,322 to 5,163.
- **Skandi's Decree** (legendary power 396, item 5900): Bursting Ice's debuff lasts 5s instead of 3s (applydebuff 1001395
  with duration_ms 5000 in AY1m6jPKTZyx4vz2 #9 and NhBnFXmy3bJg9Cav #32 and #33), pulsing on the same hasted 0.5s period
  for the whole 5s. Winter's Embrace lasts with it. Replays of those three fights went from 84.2%, 89.3% and 88.2% of the
  log's damage to 96.4%, 99.7% and 99.6%.
- **Cold Shower:** the free Ice Comet strikes 10 server frames (0.333s) after the Freezing Torrent tick that cast it,
  at any haste: Ice Comet impacts with no Ice Comet cast in the 1.6s before land 0.330-0.338s after a tick
  (4Ln6PQZCRzc7Wpta #78, jHDC613ZzbTYR8wW #18, Rzg1nQNHpm2fhYtB #24, N18KtgP3rvpYGm24 #27, GWChJ8P7tn6fHgV3 #20; haste
  22% to 64%). Avalanche's extra comets strike 5 and 11 server frames after the comet before them (0.167s, 0.367s).
- **Freezing Torrent:** a tick as the channel starts, then every hasted period, plus a partial final tick; ticks come
  one server frame after the channel starts. The final tick comes even when less than a frame of a period is left, 0-3ms
  after the regular tick at the end: 67 channels in the 15 fights (GWChJ8P7tn6fHgV3 #22 @ 116.838 and 116.839, both
  full with Supreme Torrent; NhBnFXmy3bJg9Cav #33 @ 127.877 for 817 and 127.878 for 105 without it). Freezing Torrent
  hits matched went from 7,419 to 7,474 of 7,502 and Anima mismatches before casts from 5,163 to 4,919.
- **Wrath of Winter:** the first Winter Orb comes on activation; then one every 4s hasted by the haste when the previous
  orb came. Usable during the global cooldown.
- **Ice Blitz:** not hasted.
- **Icy Flow's haste** is the hero's while the Glacial Blast that uses it casts: Bursting Ice pulses during such a cast
  come at 0.48-0.54 more haste than before it (Xaxina, GWChJ8P7tn6fHgV3 #20 @ 696.477 and 805.680, #22 @ 165.985 and
  427.146; no aura in the log). Hits matched in GWChJ8P7tn6fHgV3 #22 went from 96.3% to 97.8%, Anima mismatches before
  casts in the 15 fights from 4,919 to 4,695.
- **Haste multipliers:** a multiplying haste modifier (Shadow's Defeat, "Haste Multiplicitive 1.2") scales 1 + haste,
  the game's Haste attribute (hero baseAttributes Haste 1): Freezing Torrent ticks under it come every 0.4 (0.2857 with
  Soulfrost Torrent) seconds over 1.2 x (1 + haste) (4Ln6PQZCRzc7Wpta #77: 726.190, 0.2565s at 28.6% haste; 709.624,
  0.1902s at 24.4%; 379.487, 0.1534s with Spirit of Heroism). Matched hits in 4Ln6PQZCRzc7Wpta #77 rose from 11,615
  to 11,929.
- **Cold Snap:** its charges recharge with haste added to the recovery rate ("Cooldown Acceleration equal to your
  Haste").
- **Chronoshift:** its own 8x cooldown recovery while channeling applies to Chronoshift too; weapon abilities ignore
  other cooldown recovery (Strand of Eternity, The Monarch).
- **Utility casts:** Ice Dash logs only a cast event (no channel events) and stops no Freezing Torrent. A mount casts in
  1.5s at any haste (begincast to cast 1.504-1.517s, 8 mounts in 15 fights), Making Camp in 2.0s and Anchor in 0.667s;
  Revive's 2s is hasted (1.20-1.30s at 55-66% haste).
- **Cancelling a cast** frees the global cooldown it started (all 46 instants cast right after a cancelled Frost Bolt).
- **A Bursting Ice cast with no debuff:** 7 of 797 completed Bursting Ice casts in the 15 fights apply no 1395, no
  Winter's Embrace and no pulse to their living target, and in 5 of them the next Bursting Ice begins 1.1-7.2s later,
  inside the 10s cooldown (4Ln6PQZCRzc7Wpta #78 @ 252.915, D1fBhVPkwYKXWbpr #1 @ 123.825, GWChJ8P7tn6fHgV3 #22 @
  320.357, NhBnFXmy3bJg9Cav #32 @ 108.822, ZPTw4tmBJnK7hCb1 #4 @ 47.285). Such a cast ends with nothing done: no
  debuff, cooldown or The Wayfarer reduction. At the cast's end the target must pass the alignment filters (range 3000,
  Alive, Targetable, Hittable, NotFriendly, forward, line of sight), else the ability is cancelled before the commit of
  its cooldown and its one application, 1395 (game-data-authority, GA_Rime_CastedDebuffAoeDamage ExecuteUbergraph:
  OnFinished 1693, K2_CancelAbility 1344 / 1124, FellowshipCommitHeroAbility 1821, the application 2320). Which filter
  the 7 casts fail is not in game data. The other 790 applications come within 10ms of their cast. The replay
  cancels a completed cast with no application of 1395 within a server frame: The Wayfarer's stacks before casts went
  from 14 mismatches to 0 (all in Rzg1nQNHpm2fhYtB #24, from one such cast @ 98.722), Winter Orbs from 1,627 to 1,563,
  Frost Swallow 1033 engine-only hits from 1,634 to 1,542.

## Resources

- **Spirit Refund:** chance Spirit / (1 + Spirit); Rising Spirit comes with the spend and the orbs 0.2s later; each
  further refund adds 2s to Rising Spirit instead of restarting it.
- **Frost Swallows:** every Winter Orb earned sends 3 swallows at enemies (undocumented). Flight of the Navir's 5
  circling swallows come back when they hit, so a Cold Snap right after a Freezing Torrent tick may send only 4.
- **Wisdom of the North:** counts orbs spent, orbs that overflow, and the 5 orbs a free Glacial Assault Talon Strike
  fires "as if" spent (inferred: it explains 46 of 62 Ice Blitz cooldown misses).
- **Readings before a cast:** the log marks each ability's activation on a cast event (an instant cast, or the cast
  logged with a begincast), and a cast-time ability's completion is a cast event without the mark whose resources are
  those before its own gains and spends (GWChJ8P7tn6fHgV3 #22 @ 21.707: Frost Bolt's completion reads 5 Anima, the next
  readings 8). 333 begincasts right after the same ability's completion have no resources on their own activation, and
  the replay's check took the completion's reading; 239 of them differed in Anima. The check now reads the cast's own
  activation event: the mismatches before casts of Anima went from 4,550 to 4,305, Winter Orbs from 1,200 to 1,026 and
  Spirit from 5,464 to 5,235, with 6,733 steps checking both Anima and Winter Orbs (7,010 before).
- **A Bursting Ice pulse's Anima** comes after its damage, and only for an enemy its 1396 applied to (game-data-authority
  answer 08: GA_Rime_CastedDebuffAoeDamage DoAoeExplosion 2148-2726); the engine gave it before the damage, on every
  pulse. In a replay a pulse strikes the enemies the log's pulse struck, so an engine pulse the log has no hit for now
  gives no Anima. Bursting Ice hits matched went from 67,383 to 67,519 and Rising Talons from 5,844 to 5,912, but the
  mismatches before casts of Anima went from 4,269 to 4,311, Winter Orbs from 1,013 to 1,080 and Harrowing Ice from 830
  to 970, and Frost Swallow 1033 engine-only hits from 1,378 to 1,445 (pass-48 to pass-52). With the order alone they
  were 4,211, 1,016, 919 and 1,334 (pass-51); only the fights with Bursting Swallows change with the order.

## Gear

- **Character sheet** (combatantinfo, the 15 fights of patch 0.5.0.0): Critical Strike, Haste and Expertise equal the
  gear's attributes plus the stat enchants (each gear trait's rank; 6 per attuned weapon-tree node at item level 315)
  plus the flat gem nodes (8 rating at 150 power, 24 at 800, rank II replacing rank I) in every fight. Spirit is 7 above
  that sum in AY1m6jPKTZyx4vz2 #9 and jHDC613ZzbTYR8wW #18 and 11 above in N18KtgP3rvpYGm24 #27 and Rzg1nQNHpm2fhYtB #23
  and #24. Intellect over (hero baseAttributes Intellect 100 + gear Intellect + Champion's Heart + Stoic's Teachings) is
  1.100 without Ruby (GWChJ8P7tn6fHgV3 #20, #22: Mote of Creation, effect 3085, Additive 0.1), 1.122-1.126 with Might of
  the Minotaur (node 296), 1.146-1.149 with Ancient's Wisdom as well (AY1m6jPKTZyx4vz2 #9, Rzg1nQNHpm2fhYtB #23, #24) and
  1.165 with Might of the Minotaur - II (NhBnFXmy3bJg9Cav #32, #33). So the sheet's Intellect holds Might of the
  Minotaur, whose first logged event is its removal in every Ruby fight. The replay takes it out of the sheet and gives it
  back from the log's events: the median log/engine of each Ruby player's matched hits went from 0.973-0.993 per fight to
  0.992-1.013 (Might of the Minotaur - II: 0.962 and 0.986 to 1.020 and 1.045); Xaxina's stayed at 1.000.

- **Anomalous Residue** (affix, effects 2704-2708 and 2710): each stack adds 480 to one colour's gem power for 60s, up
  to 3 stacks, and Covetous Greed's 1.25 scales it (effect 3271 multiplies after the Additive sum). As the power changes
  the game re-evaluates the colour's nodes at once, the highest five at or below the power (UCRGemSubsystem 0x145388B10,
  callback 0x145390CE0), and the overcap bonus follows it (Main Stat Multiplicitive 1 + 0.00005 per point over 1500,
  effect 2469). The log shows the swaps within 2ms of the stack (D1fBhVPkwYKXWbpr #1 @ 151.646: Ruby stack, Might of the
  Minotaur removed, - II applied). It is on the player in 6 of the 15 fights; the replay changes each gem's power with
  the log's stacks and follows Might of the Minotaur and Blessing of the Conqueror at both ranks. Exact replay of
  4Ln6PQZCRzc7Wpta #78, D1fBhVPkwYKXWbpr #1, NhBnFXmy3bJg9Cav #32 and #33, Rzg1nQNHpm2fhYtB #23 and ZPTw4tmBJnK7hCb1 #4
  went from 94.0%, 95.7%, 97.7%, 95.6%, 97.2% and 93.5% to 97.6%, 97.6%, 99.5%, 98.4%, 99.3% and 98.5%; matched hits
  where a Ruby player's engine has no Might of the Minotaur went from 1.06-1.15 of the engine (median) to 0.99-1.01.

- **Kindling** (sigil 28, ability 172, effect 104): 2.1 PPM, 0.69 / 1.26 / 1.83 / 2.40 x Main Stat per hasted 1.5s tick
  for 9s, a partial final tick, and a proc on a burning enemy restarts the 9s and keeps the tick timer (rows
  Trait.ExtraDotHotOnEffectApplicationProc.*, effect 104's stacking policies). It rolls on each application of a harmful
  effect (a hit, Bursting Ice's debuff), not on a damage-over-time tick; the replay takes a proc from the log's
  application or refresh of the burn on the enemy. Kindling hits matched went from 934 to 1,560 of 1,660 and engine-only
  ones from 902 to 23; calibration 1.279 / 1.244 to 1.016 / 1.034 (Slick, Xaxina and Loreuxe).

- **Harmonious Soul** (Diamond nodes 338 and 339, effects 1959 and 1960): each enemy death gives a stack and restarts
  the 5s; each expiry takes one stack and restarts the 5s (removebuffstack every 5.01s); an enemy that leaves combat
  alive gives none. The engine's stacks equal the log's before every cast in the four Diamond fights without Anomalous
  Residue (4Ln6PQZCRzc7Wpta #77, AY1m6jPKTZyx4vz2 #9, jHDC613ZzbTYR8wW #18, Rzg1nQNHpm2fhYtB #24).

- **The Wayfarer** (glyph 703): the time to the next application is effect 3183 (60s). Each CORE cast takes its
  cooldown x 4 / 30 off it as the cast completes (Bursting Ice 1.333s, Freezing Torrent 2.0s), and a cast that takes it
  past 0 applies The Wayfarer at once: 13 cycles of N18KtgP3rvpYGm24 #27 and Rzg1nQNHpm2fhYtB #23 match to 0.03s. The
  cooldown carries over between pulls: it runs out 34-44s into N18KtgP3rvpYGm24 #27 and Rzg1nQNHpm2fhYtB #23 and #24
  with no application before, so the replay keeps it until the log's first removal. The Wayfarer's stacks before casts
  went from about 140 mismatches to 14.

- **The Sinister** (glyph 701): Bursting Ice's pulses (effect 1396) get neither its crit chance nor its crit power;
  Freezing Torrent's ticks get both. Crit over normal, log against engine with them: Bursting Ice 0.830 at rank 4 (Slick,
  4Ln6PQZCRzc7Wpta #77 and #78) and 0.951 at rank 1 (Loreuxe, jHDC613ZzbTYR8wW #18), Freezing Torrent 1.013 and 0.997;
  after: Bursting Ice 1.013 / 1.018 and 0.971 / 0.971 (crit / normal).

## Not yet explained

Numbers from the 15 fights of patch 0.5.0.0 (`docs/accuracy/rime-accuracy-v1.md`).

- **Per fight** (final measure): exact replay 97.4% to 99.6% of the log's damage (7 of 15 fights outside 99-101%),
  97.9% of log hits matched; the missing 2.1% of the hits (mostly Bursting Ice pulses and Frost Swallows) are most of
  what each fight lacks. The median log/engine of matched hits is 0.999-1.014 per fight (inmy: 1.011 and 1.009 in
  NhBnFXmy3bJg9Cav #32 and #33). Rime's primary-stat buffs are as the engine sums them (Willful Momentum 2078, Vengeful
  Soul 2163, Power Revealed 2165, Martial Initiative 2174: all PrimaryAttributeMultiplier Multiplicitive).
- **inmy's Bursting Ice and Harrowing Ice** (pass-22; with the repeated pulse in the engine, the median log/engine of
  inmy's matched hits went from 1.018 and 1.023 to 1.011 and 1.009): inmy's excess is in Bursting Ice alone. In
  NhBnFXmy3bJg9Cav #33 the other abilities are 0.998-1.010 of the engine all fight; Bursting Ice is 1.001 in the first 150s, 1.028-1.044 to 900s and
  1.052-1.089 after (#32: 0.998, then 1.023-1.078 to 600s). inmy is the one Rime player with Harrowing Ice (talent 627, 3% per
  stack, reset at 31), and has Bursting Swallows, whose pulses the engine partly misses. A missed pulse puts the engine's
  Harrowing Ice count one behind the log's, and the lag carries over each reset. By the log's stacks at the hit, #33's
  Bursting Ice is 1.024-1.031 before 600s and 1.063-1.085 after at 5-30 stacks, the ratios of a lag of about 1 stack
  and then 3 to 4 (1.02-1.03 and 1.05-1.09); at 0-4 stacks after 600s it is 0.629, the log having reset first. The
  ratio does not follow the targets per pulse (1.003-1.080 for 1-5 to 13+ targets). Sources checked for gem power:
  Covetous Greed 3271, Seal of the Heskyr 3195 (all six gem powers MultiplyCompound 1.25, a System effect no record
  grants, its aura on no Rime player), Anomalous Residue 2704-2710, the gem power overcap 2469.
- **Amethyst Splinter** (1000101, A29, A43): normal calibration 1.087, 30,963 of 31,577 log hits matched (98.1%). On the
  log's own crits the engine's model (a crit adds its share to the damage left and restarts the 8s; hasted 2s ticks)
  gives a median log/model tick of 1.000 and a mean of 0.99 (GWChJ8P7tn6fHgV3 #20, D1fBhVPkwYKXWbpr #1,
  4Ln6PQZCRzc7Wpta #78). A crit mid-period keeps the tick rhythm: 20,126 refreshes in the logs keep it, 14 restart it.
  The matched ticks' summed damage is 1.024 of the engine's; the calibration is a per-hit mean. 14.0% of the matched
  ticks pair a log tick and an engine tick more than 0.5s apart (mean 1.312, median 1.028); the others have a mean of
  1.050 and a median of 1.013. The excess comes from the splinter's inputs: 2% of ticks above 2x contribute 0.075 of
  the mean, ticks on enemies whose crits the engine misses (GWChJ8P7tn6fHgV3 #20 enemy 96.2 at 48.4: a 4,180
  Coalescing Frost crit the engine never dealt). Sources checked: aspect 31; effect 101 and ability 170
  (game-data-authority: tick = (crit damage x share + damage left) / (Duration 8 / TickRate 2) as SetByCaller.Damage,
  with no further multiplier; rows Trait.GemDotHotOnCrit.Debuff.DamagePercentage, Duration, TickRate).
- **Glacial Blast crits** (1028, A34): crit 1.024 against normal 1.003 (final measure). At pass-20, Wobra's Glacial
  Blast crits where the engine has
  no Frostweaver's Wrath are 1.073 and 1.104 (D1fBhVPkwYKXWbpr #1, 54 hits; ZPTw4tmBJnK7hCb1 #4, 11) of the engine; the
  log has the buff on 34 and 12 Glacial Blast hits, the engine on 25 and 9. Of D1fBhVPkwYKXWbpr #1's 107 Frostweaver's
  Wrath gains, 81 come within a server frame of an engine Winter Orb gain and the engine finds them; the engine's nearest
  orb gain is 0.1-0.3s from 12 of the others and 0.3-1s from 13 (the Anima drift below). Wobra's normal Glacial Blast
  right after one that used the buff (0.87-1.17s after its removal) is 1.03-1.32 of the engine, 1.112 on average over
  D1fBhVPkwYKXWbpr #1's 10, against 0.94-1.07 for most of the others; nothing in effect 1392, Frostweaver's Wrath or
  Greater Glacial Blast carries over to the next cast (game-data-authority, ability 1028 and 1571 graphs). Sources
  checked: talent 191, hero_data ChanceOnOrbGain, effects 1392 and 2305, abilities 1028 and 1571.
- **Frost Swallows hit counts** (A41, A46, final measure): Frost Swallows 1033 95.7% matched with 1,388 engine-only
  hits (4.6%), 1001365 95.1% with 207 (3.0%). Bursting Ice has 67,383 of 68,700 (98.1%) matched and no engine-only
  hit; the missing ones are mostly whole pulses in builds with Bursting Swallows, whose pulses follow Frost Swallow hits
  the engine does not match, and the repeated pulses below.
  Engine-only swallows come three at a time on Winter Orbs the log does not have at that moment; per fight the engine's
  swallow count is within 3% of the log's, and matched swallows land 0.3-1.9s after their release. The Anima and Winter
  Orbs before casts (A49, A51) differ in 52% and 14% of steps; Frost Bolts that never land (4Ln6PQZCRzc7Wpta #77 @
  116.780: one +3 Anima for two completed casts) start drifts. Bursting Ice gives 1 Anima per pulse
  (Rime.CastedDebuffAoeDamage.ResourceGainPerHit 1, AoeResourceRewardCap 1). Causes of drift the engine now has:
  Freezing Torrent's final tick, Icy Flow's haste during the Glacial Blast cast, Bursting Ice ending with its enemy, the
  repeated Bursting Ice pulse and the frame a haste change before the frame's timers adds.
- **Bursting Ice's repeats the engine lacks** (pass-31): in the 5 fights without Bursting Swallows, 14 of the log's 36
  repeated pulses have no repeat in the engine. Adrenaline Rush comes with the hit that takes an enemy to 30% health or
  less, and the engine's pulse comes up to a frame before the log's: the game fires a timer on the first server frame
  after it is due (FTimerManager::Tick 0x144056E90, game-data-binary), and in the 5 fights the first hits of 1,676 of
  2,590 matched pulses are 0-35ms later in the log than in the engine (median 18ms). The enemy's health in the engine
  is then from before the log's crossing: of the log's 58 Adrenaline Rush gains inside a pulse in the 15 fights, 18 are
  in the same pulse in the engine (GWChJ8P7tn6fHgV3 #20: the engine's pulse at 48.040, the log's at 48.059, the
  engine's gain at 48.363). game-data-binary has a second execution in one frame at the debuff's expiry (CheckDuration
  0x144A6E990), which 30 of the log's 449 repeats in the 15 fights fit; the engine has none.
- **Server frames** (game-data-binary: the server runs every timer in FTimerManager::Tick 0x144056E90 on the first frame
  after it is due, InternalTime > ExpireTime at 0x144056F93): Rime's own events come in frames 33.39-33.44ms of log time
  apart (GWChJ8P7tn6fHgV3 #20, jHDC613ZzbTYR8wW #18). In the 5 fights without Bursting Swallows, 79% of 2,203 timer
  pulses (no partial or Burstbolter pulse) are logged on the first frame after the engine's due time (GWChJ8P7tn6fHgV3
  #20 87%, #22 88%) and 13% a frame later, the later share rising from 6% under 0.7s after the application to 18% at
  2.1-3.2s. With the engine's period ticks on those frames (each due time kept, the tick on the first frame after it),
  80.6% of those pulses come within 5ms of the log's against 10.3% (72.3% against 14.6% in the other 10 fights) and
  Bursting Ice hits matched went from 67,383 to 67,502, but Frost Swallow 1033 engine-only hits went from 1,388 to 1,570
  and the mismatches before casts of Anima from 4,305 to 4,522 and Winter Orbs from 1,026 to 1,129 (pass-36 to
  pass-44v1). Holding a frame's ticks until a cast that opens the frame has run gave 4,595 for Anima; the engine keeps
  its ticks at their due time. Of the debuffs running their full 3s, 580 of 586 with 3 or more frames from the last
  pulse to the removal have a partial pulse, and 29 of 146 with 1 or 2 frames; the removal comes 3-20ms after the
  application plus 3s in 711 of 739. The first pulse after an application is logged on the first frame after the
  engine's due time in 695 of 781 applications (pass-48), a frame earlier in 56 and one or two frames later in 29
  (12 fights; 12 of them in Rzg1nQNHpm2fhYtB #23 and #24, among them #23 @ 77.914, 100.285 and 373.444, whose whole
  chain runs a frame behind the engine's). 24 of the 29 have another cast in the application's frame, as do 630 of the
  752 others; 12 of the 29 have a Focused Haste reset or stack change there, against 219 of the 752; only #23 @ 373.444
  loses a haste buff (Ancestral Surge - II, 1001920) a frame after the application. The Bursting Ice cast completes in
  the millisecond of the application in all 29. At #23 @ 373.444 the log's pulses sit on the frames (373.810 and
  374.111 with the Freezing Torrent tick at 374.110), so the first is due after 373.777; the engine re-arms at the loss
  @ 373.477 to 373.754, and a re-arm that read the time left against the application's frame would give 373.790. MinimumPartialTickTime 0.1s (game-data-authority answer 10) gives the main split;
  the 29 of 146, the 11 of 13 with a gap of 70-79ms and the 6 of 586 without a pulse wait on which period 0x1452D0C70
  compares, and whether the server's frame time is fixed or measured (game-data-binary). Ending the debuff at the start of that frame took
  Winter's Embrace off 2,961 matched hits logged in it.
- **Bursting Ice's pulses after a haste change** (pass-33, the 5 fights without Bursting Swallows): after a Focused Haste
  change from an instant cast, the log's lag behind the engine shrinks by a frame in 17 of 60 pulse pairs, against 21%
  with no haste change; the 17 include instant casts with no other event of the player in their frame
  (jHDC613ZzbTYR8wW #18 @ 191.145) and casts in a frame with a Frost Swallow hit (Rzg1nQNHpm2fhYtB #24 @ 136.458). A
  change from another timer earlier in the frame of a due pulse makes the time left negative, and the re-arm then skips
  that pulse for a full new period (InternalSetTimer 0x144041EEF); the engine has no frame order between timers. Of
  2,098 pulse gaps in 265 debuffs, 6 are over 1.5 times their debuff's median gap, one of them with a haste change in
  the frame of the due pulse (GWChJ8P7tn6fHgV3 #20 @ 215.966, Wrath of Winter's completion with Spirit of Heroism).
- **Resources and auras before casts** (A49-A58), the mismatches of 8,830 steps (baseline, pass-05, pass-22, final; the
  final's resources read from each cast's own activation event): Anima 5,500, 5,362, 4,872, 4,305; Spirit 5,404, 5,518,
  5,467, 5,235; Winter Orbs 2,169, 1,679, 1,627, 1,026; Harrowing Ice 1,240, 991, 1,005, 830; Seized Opportunity's
  counter (2111) 1,001, 1,212, 1,010, 965 and buff (2110) 380, 640, 548, 541; Frostwyrm's Spite 424, 150, 47, 44; The
  Wayfarer 862, 14, 14, 0; Hidden Power 1,096, 0, 0, 0; The Vehement 648, 0, 0, 0. As engine and log total Anima
  (Winter Orbs x 9 + Anima) before casts, 1,977 of the 4,391 mismatched steps (of 6,733 that read both) differ by one,
  the rest by more (a pulse, tick or Frost Bolt the engine has and the log has not, or the reverse); 23 of the 1,132
  steps one above the log have an engine pulse in the cast's server frame whose log pulse comes after the cast. Of the
  changes of the difference between two checked steps, 289 (1,340 steps after them) have no hit issue between the
  steps; in Rzg1nQNHpm2fhYtB #24 @ 73.613 and 86.100 the engine's pulse comes 41ms and 51ms before the Ice Comet and the
  log's after it. A drift lasts long: after GWChJ8P7tn6fHgV3 #20's skipped pulse @ 215.966,
  129 of the steps to 432s have the engine one Anima above the log. Harrowing Ice and Seized Opportunity count the
  pulses and crits the engine deals, so they follow the missing pulses and swallows; Frostwyrm's Spite counts Freezing
  Torrent ticks (27 of 7,502 missing).
- **Spirit Points** (A50): the engine runs 9 to 29 points above the log in 12 fights and 5 to 10 below in
  GWChJ8P7tn6fHgV3 #20 and #22. The log gains none before combat (N18KtgP3rvpYGm24 #27: 0 at 0.844, 2 at 12.614) and its
  1-per-3s regeneration ticks about 1s off the engine's (20.0, 23.1, 26.1 against 21, 24, 27). Regenerating only with
  enemies present closed part of it and overshot elsewhere (Rzg1nQNHpm2fhYtB #23 +8.9 to -6.6), so it is not in the
  engine. The game's gain (game-data-authority Q12, ability 1007 and effect 1351): each party member's damage gives
  every member SpiritPointValue x the share of the target's maximum health x 0.25, x4 once the dungeon timer has
  expired, clamped to [0, MaxSpiritPoints]. The regeneration's timer is not yet found (rows Hero.Spirit.PassiveGainRate
  1 and PassiveGainPeriod 3; no reader in the Blueprints or the binary). The gain needs every party member's damage,
  outside the events of Rime's players; x0.25 on Rime's damage alone puts the engine far below the log.
- **Curse of An'zhyr's tick timing** (sww, Rzg1nQNHpm2fhYtB #23 and #24, pass-48): the log's tick comes a median 166ms
  after the engine's (#23 190ms, #24 134ms; 19.3% of 2,385 matched ticks within 50ms; 68 more pair a whole period off).
  On each enemy the lag starts at 5-77ms and grows over the curse's life: over 81 runs of 12 or more ticks on one enemy
  it changes by a median 4.95ms a second (-5.9 to +12.0), the engine's period about 0.5% shorter than the log's (the
  boss 74.1 in #24: 9ms at 538s, 292ms at 664s, 233ms at 690s). At 568-586 in #23 it runs the other way: on enemy
  18.3 the curse ticks every 1.50-1.60s in the log to 577.6 and every 1.69-1.81s in the engine, and every 1.24-1.44s
  against 1.36-1.61s to 586, a Haste 0.16-0.24 below the log's (3s over each interval), before and after the curse
  moved to the period timer (pass-35, pass-36). Sources checked: game-data-authority answer 09 (effect 2286's period,
  tags, stacking and duration). The log's clock (UTC at each event's write, FDateTime::UtcNow 0x145274AE0) and the
  timers' world time (the sum of measured frame deltas, 0x14405AAB5) both count real time (game-data-binary), and the
  drift is not one rate for every timer: the median slope per chain (pass-52) is +4.8ms a second for the curse and -3.2 for Bursting
  Ice's pulses in #23, +5.2 for the curse in #24, -0.4 to +3.7 for Amethyst Splinter and -0.4 to +1.2 for Kindling in
  the other fights, against frame spacings of 1.7-2.3ms a second over 1/30s.
- **Rain of Blood and Orb Energy** (2896, 2437): summed per stack, hits under them are 0.95-0.97 of the engine; both fit
  (1 + X + 0.05 n) / (1 + X) with X about 0.09. How the game combines a Multiplicitive DamageScale modifier with stacks
  is not yet checked in the game data.
- **The Wayfarer out of combat**: effect 3183 has no combat condition in the game data, so its cooldown runs out of
  combat. A gap between pulls in the spec's fights long enough to show it is not yet found.
- **Frost Bolts that never land**: 12 of 1,248 casts. Frost Bolt's 3 Anima come from ability 1033 after the cast's
  commit, a bolt spawned or not; a cast whose target fails the alignment filter as it finishes, or whose commit fails,
  ends with no bolt and no Anima (game-data-authority, GA_Rime_CastedProjectileDamageFiller 3586-3782). Which of the
  two the 12 casts are is not yet found.

## Not yet checked against a log

Icy Flow's crit bonus (+20% in the talent, +25% on the buff), whether Frostweaver's Wrath's 20% rolls per Winter Orb or
per gain (155 stacks for 848 orbs in 685 rises in D1fBhVPkwYKXWbpr #1 and ZPTw4tmBJnK7hCb1 #4) and Soulfrost Torrent's
1.2 procs per minute are `UNVERIFIED`.
