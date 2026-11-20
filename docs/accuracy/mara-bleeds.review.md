# Adversarial review: mara-bleeds

Branch `mara-bleeds` at `a0c63984`, pull request SimFellDev/SimFell#61, hero Mara, the 34 fights of
`docs/accuracy/mara-bleeds.md`. Each claim on the branch was taken as false until this session's own evidence gave it.

Evidence:

- **Logs.** Every event with a Mara player as source or target, from the 34 raw fights of `data/fights`.
- **Engine state.** `MaraReplayTests` at the branch head with `MARA_HITS_FOLDER`: each hit's engine amount, Haste and enemy
  auras, joined to its log event by time, damage ID and enemy.
- **Game data.** Five fresh `game-data-authority` runs in an extractor worktree from `master`, build 26320967. Each run had
  the subject and the question, with no value and no claimed source.
- **Measure.** `HeroAccuracyTests` with label `review`, 100 free-dice runs a fight.

| Kind | Findings |
| --- | --- |
| Fitted constant | 0 |
| Masking | 2 |
| Logic error | 3 |
| Wrong number | 2 |
| Unsubstantiated claim | 8 |
| Regression | 0 |
| Contamination | 0 |

## Masking

### M1. The mitigated part is added to hits the engine has already reduced

- **Kind:** masking.
- **Place:** `src/FellowshipSim.Engine/Replay/ReplayScript.cs:160` (`UnblockedAmount => TotalAmount + Math.Max(Blocked, Mitigated)`).
- **Claim:** the log's `mitigated` is the enemy's damage reduction, the engine has none, and the engine's hits match the
  amount plus the mitigated part (`ReplayScript.cs:155-158`, `NOTES.md:54-62`, commit `35956983`).
- **Evidence:** 1,641 of the 538,847 Mara hits have a mitigated part and no block. Joined to the engine's hits:

  | Mitigated share of the hit | Hits | Fights | amount / engine | (amount + mitigated) / engine |
  | --- | --- | --- | --- | --- |
  | 45% to 55% | 1,242 | 6 | 0.500 | 0.928 |
  | 70% to 90% | 245 | 10 | 0.770 | 3.385 |
  | 90% to 100% | 134 | 16 | 0.020 | 1.084 |
  | other | 14 | 7 | 0.555 | 0.938 |

  - **Greed's Embrace.** 242 of the mitigated hits are on an enemy with Greed's Embrace (effect 649). The engine has that
    aura's `IncomingDamageMultiplier` 0.2 (`PartyBuffs.cs:79-82`), so its hit is the reduced one: amount / engine is 1.008,
    and (amount + mitigated) / engine is 4.477. 239 of the 242 compare at over 1.5 times the engine. The fights are
    3tM2NRFDCnhkqdx6 fight 7 (7 hits), CwmX2Tb8GA6hyc1v fight 8 (45), gd6ByNrf81jc9aWw fight 3 (40), mThk4v1f8WGbBd7Q
    fight 1 (12), pWmcV42K39GHTLQX fight 23 (61) and za2QymCwxMcqfH16 fight 20 (75). Their mitigated sums are 0.06% to
    0.29% of each fight's log amount, and the exact replay of three of them fell by that much from the baseline:
    za2QymCwxMcqfH16 fight 20 99.4% to 99.1%, CwmX2Tb8GA6hyc1v fight 8 99.9% to 99.6%, pWmcV42K39GHTLQX fight 23 99.8% to
    99.6%.
  - **The 45% to 55% windows, by ability.** Volatile Poison 1.001 (557 hits), Skittering Blades 1.003 (229) and Attack
    1.013 (14) match amount plus mitigated. Arachnid Assault is 0.918 (402 hits) and Queen's Fang 0.922 (22), and their
    amount / engine is 0.499 and 0.501: the log's mitigated part of a finisher is 0.84 of its amount, and twice the amount
    is the engine's hit.
  - **90% to 100%.** Amethyst Splinter 1.269 (22 hits) and Vexira's Venom 1.328 (18); the direct hits are 0.85 to 1.03.
- **In its place:** the mitigated part equals the engine's missing reduction for Volatile Poison, Skittering Blades and
  Attack in the two Silken Hollow fights' windows. For an enemy aura in `PartyBuffs` the engine's hit compares with the
  amount alone. For Arachnid Assault and Queen's Fang in those windows the comparison is 8% low with no source for the
  difference.

### M2. Hits matched for Hemorrhage, Seething Poison and Hemotoxin's poison come from the log's tick times

- **Kind:** masking.
- **Place:** `src/FellowshipSim.Core/Heroes/Mara/Abilities/MaraDot.cs:149` (`PeriodicEffect.BeginHasted` with
  `damageLogId` and `target`), `Hemorrhage.cs:34`, `SeethingPoison.cs:29`, `Hemotoxin.cs:62`.
- **Claim:** the period timer of a damage over time takes Hemorrhage from 5,666 to 5,989 matched of 6,117, and fight 26
  from 13,465 to 13,741 matched and 96.7% to 98.1% (`NOTES.md:140-147`, commit `a0c63984`, the pull request body).
- **Evidence:**
  - `LogOutcomeSource.TickAt` (`LogOutcomeSource.cs:224-273`) gives each tick the time of the nearest unclaimed log tick
    within half a period, and the next period runs from there. A period error under 50% leaves the hit matched.
  - The baseline has no tick time issue for these three damage IDs. The review measure has 2,606 `tick time: Hemorrhage`
    issues against 5,989 matched Hemorrhage hits, and 1,180 `tick time: Seething Poison` against 10,007.
  - fjrCax7VzGmMZw6h fight 26: 2,097 of its 2,493 matched Hemorrhage ticks are over one server frame from the engine's
    time. The engine's copies have period scaler 0.725. The log's copies are 0.632 of 3s / Haste apart at the median
    (1,610 tick pairs with no bleed event between them and equal engine Haste at both; 93.2% within 0.03 of 0.631, 6.2%
    within 0.03 of 0.725).
  - `NOTES.md:475-477` gives the exact replay of fight 26 with scaler 0.631 as 13,768 hits and 2,520 Hemorrhage hits
    matched. The head has 13,741 and 2,493 with scaler 0.725 in the engine.
- **In its place:** the game data has `EffectType.Dot` on effects 1724, 1716 and 2337, with no Haste or partial tick
  exemption, so the period timer has its source. Hits matched for these three IDs is a count of log ticks reserved. The
  period model's measure is the tick time issue count: Hemorrhage 2,606 of 5,989 (fight 26: 2,097 of 2,493), Seething
  Poison 1,180 of 10,007.

## Logic error

### L1. Gushing Blood copies the bleed to enemies at any distance

- **Kind:** logic error (a talent's effect outside its condition).
- **Place:** `src/FellowshipSim.Core/Heroes/Mara/Talents/GushingBlood.cs:28-30`.
- **Claim:** the copy goes on up to 4 other enemies, those without Mara's bleed first; the engine has no positions and takes
  the enemies in its own order (`GushingBlood.cs:13`, `NOTES.md:165`).
- **Evidence:**
  - Game data, ability 1517: `Cylinder Radius` row `...BleedAttackSpenderBleedAoe.MaxRadius` 600, `MaxNumberOfTargets` 4
    with `OrderType` `Closest`, then an angle filter of 270 degrees after the cap (offset 634), so fewer than 4 can remain.
    The centre of the cylinder and the preference of `PreferActorsWithoutEffect` are native and unread.
  - fjrCax7VzGmMZw6h fight 26, the 115 re-applications by other enemies reached: 0 in 41, 1 in 1, 2 in 11, 3 in 11, 4 in 51.
  - The engine copies to 4 whenever 4 other enemies are active. In fight 26's exact replay 253 engine Hemorrhage hits
    have no log hit, and 62 Hemorrhage effects have a tick count other than the log's.
  - From the Shadows' rolls in the measure's exact replays rose from 9,129 at the baseline to 12,850 (L2).
- **In its place:** the copy count has a radius of 600 and an angle of 270 degrees in the game data, and the logged
  distribution above. `NOTES.md` has the limit in its Timing section and no line under "Not yet explained" with these
  numbers.

### L2. From the Shadows' proc is 4.2 times the log's, and it is the free dice gain of the pull request

- **Kind:** logic error (a proc's roll set).
- **Place:** `src/FellowshipSim.Core/Customization/Legendaries/FromTheShadows.cs:27-31`; the branch's doc comment at
  `FromTheShadows.cs:6-15` replaces `UNVERIFIED: no combat log has it`.
- **Claim:** the legendary's mechanics have game data and fight 26 as their sources; free dice mean / log goes from 92.8%
  to 93.2% (pull request body).
- **Evidence:**
  - Measure, exact replay, proc `from_the_shadows`: 12,850 rolls, 1,927.5 procs expected at chance 0.15, 459 procs in
    the log. The baseline has 9,129 rolls, 1,369.4 expected, 347 in the log.
  - 459 procs against the 2,614 logged Hemorrhage ticks of fight 26 is 0.176 a tick; against the engine's rolls it is
    0.036 a roll.
  - Free dice, fight 26, seed 1 (`MARA_FREEDICE_SEED=1`): Queen's Fang 2,498 engine hits and 18,907,222 damage against
    625 hits and 4,048,633 in the log.
  - Fight 26's free dice / log is 122.0% at the baseline and 144.8% at the head, with the log at the 0% point of the
    engine's runs in both.
  - The free dice headline without fight 26: 92.21% at the baseline, 92.02% at the head. The whole gain from 92.80% to
    93.17% is fight 26's engine damage rising from 31,499,486 to 37,385,368 against a log of 25,816,753.
- **In its place:** the roll set of the proc has no log support: one fight has the legendary, and its proc count is near
  0.15 a bleed tick. Free dice over the other 33 fights fell by 0.19 points. Fight 26's free dice has no line in the pull
  request body or under "Not yet explained".

### L3. Conjure Portal and Rejuvenate have no relic cooldown gem factor

- **Kind:** logic error (a cooldown rule).
- **Place:** `src/FellowshipSim.Core/Customization/CommonAbilities/ConjurePortal.cs:18`, `Rejuvenate.cs:19`.
- **Claim:** each has a 120s cooldown from the game data; Bloodrite Fervor's cooldown is its row times the relic cooldown
  gem factor (`BloodriteFervor.cs:53-62`).
- **Evidence:** game data, build 26320967: `GA_RelicPower_Portal` and `GA_RelicPower_HealthPotion` both have
  `CooldownEffectBaseClass` `GE_Shared_Cooldown_RelicScale`, whose duration is `GMMC_GemAugmentation`, the calculation
  Bloodrite Fervor's cooldown has: 1 - 0.08 with `GE_GemBonus_RelicCooldownReductionBonusSmall`, 1 - 0.24 with the Medium
  effect, else 1. The logs have no case: the one Mara player with Diamond power 450 or more and a Conjure Portal cast
  (HgamNcX24tJ3qVQA fight 21, Diamond 500) has its two casts 672.650s apart.
- **In its place:** Conjure Portal's cooldown is `RelicPower.Portal.Cooldown` (120) times the gem factor, and Rejuvenate's
  is `RelicPower.HealthPotion.Cooldown` (120) times the gem factor.

## Wrong number

### W1. Hemorrhage's counts in the pull request body

- **Kind:** wrong number.
- **Place:** pull request body, Results: "Hemorrhage hits matched 5,666 → 5,989 of 6,117, engine-only 757 → 412", under
  a Baseline and Final table. The same numbers are at `NOTES.md:146` and in commit `a0c63984`.
- **Evidence:** the measure's Hemorrhage row is 4,982 matched and 274 engine-only at the baseline
  (`mara-bleeds.baseline.txt:54`), and 5,989 matched and 284 engine-only at the head. 5,666 matched and 306 engine-only
  are pass-04's. 757 and 412 are sums of the `engine-only` column of `MaraReplayTests` (`MaraReplayTests.cs:89`), engine
  hits without a landed log hit: 306 + 451 at pass-04 and 284 + 128 at the head, the second term being the log hits the
  engine never produced.
- **In its place:** Hemorrhage matched 4,982 → 5,989 of 6,117 from the baseline, and engine-only 274 → 284.

### W2. Seething Poison's counts

- **Kind:** wrong number.
- **Place:** `NOTES.md:147` and commit `a0c63984`: "Seething Poison 9,906 to 9,931 of 9,933 and 187 to 153".
- **Evidence:** the measure has 10,009 Seething Poison log hits: 9,982 matched and 160 engine-only at the baseline and at
  pass-04, 10,007 and 151 at the head. The sum of `MaraReplayTests`' column at the head is 153 (151 + 2).
- **In its place:** Seething Poison matched 9,982 → 10,007 of 10,009, engine-only 160 → 151.

## Unsubstantiated claim

### U1. Hemorrhage on enemies without Seething Poison has no line in NOTES.md

- **Place:** pull request body, Open: "Hemorrhage ticks are about 1.18 × the engine's on enemies without Seething Poison.
  No source found yet."
- **Evidence:** the number holds. Full Hemorrhage ticks, log / engine: 1.182 crit (1,523 ticks) and 1.175 normal (1,400)
  without Seething Poison on the enemy, 0.982 and 0.982 with it (1,721 and 1,140). Fights with 20 or more ticks without
  it are 1.160 to 1.254, Fc8v6yWxD14GQMnV fight 19 apart at 1.064. `NOTES.md` has no line for it; `docs/REPLAY_ACCURACY.md`
  gives such a difference a line under "Not yet explained" with its numbers and every source checked. The measure's
  Hemorrhage calibration is 1.090 normal and 1.077 crit, against 1.064 and 1.053 at the baseline.

### U2. Bloodrite Fervor's cooldown "reads node 5 alone"

- **Place:** `NOTES.md:498-501`, commit `e3d54716`; code at `BloodriteFervor.cs:60`.
- **Evidence:** `NodeInSlot(5)` is the active node of slot 5, and `GemNode.Slot` is `(Node - 1) % 5 + 1` (`Gem.cs:20`), so
  at Diamond power 1500 it is node 10 and the factor is 1 - 0.24. Game data: `CalculateBaseMagnitude` tests the Small
  effect first (offsets 663 to 810), then the Medium (1012 to 1130), and the removal of node 5's effect at node 10 is
  unread, so the factor at 1500 is 0.92 or 0.76. No log has the factor: the five Mara players with a Bloodrite Fervor cast
  all have Diamond power 0 (3M2vWr8taxVfTQFY fight 53, b649dhNptyW132Tm fight 5, fjrCax7VzGmMZw6h fight 26,
  NXQnyLMF1aPrkCf2 fight 5, X82J6bGRNBracxLg fight 7), so the 90.1s shortest gap is the base cooldown's.
- **In its place:** the engine's factor is 0.92 from 450 and 0.76 from 1500, the second with no decoded source.

### U3. The mitigation sentences' counts

- **Place:** `NOTES.md:54-62`, `ReplayScript.cs:144-158`, commit `35956983`.
- **Evidence:**
  - "On a blocked hit it is the blocked part and 1": of the 583 blocked hits, 283 have mitigated = blocked + 1, 295 have
    mitigated = blocked, and 5 have mitigated below blocked.
  - "1,104 of Mara's 16,995 hits with 45% to 55% mitigated": gd6ByNrf81jc9aWw fight 3 has 1,104 hits with a mitigated
    part and no block; 1,063 of them are 45% to 55%, and 40 are on an enemy with Greed's Embrace (M1).
  - "On every enemy hit at 260 to 280 s, 300 to 320 s, 360 to 380 s and 420 to 440 s": 1,053 of the 3,967 hits in those
    four windows have a mitigated part, and 51 mitigated hits are outside them (20 to 160 s, 440 to 460 s).
  - "Amount and mitigated make up the log's `unmitigatedAmount`": true of every mitigated hit but one. Over all 538,847
    hits it fails for 8,091: 7,621 with unmitigated below amount, 469 with an absorb, 1 with overkill; 1,114 have no
    `unmitigatedAmount`.
  - 3tM2NRFDCnhkqdx6 fight 7's 185, the block at Fc8v6yWxD14GQMnV fight 19 @ 32.008 and the hit at @ 303.867 hold.

### U4. Gushing Blood's re-application counts

- **Place:** `GushingBlood.cs:16-18`, `NOTES.md:163-165`.
- **Evidence:** fjrCax7VzGmMZw6h fight 26 has 154 Hemorrhaging Strike casts. 115 have a removal and an application of
  Hemorrhage in the cast's frame (claim: 114), all 115 in Maiden of Death, and none of the 39 casts outside it has bleed
  events on a second enemy. The struck enemy had the bleed in 109 and had none in 6 (claim: 108 and 6). 41 reach no
  other enemy (claim: 62). Every copy of each of the 115 has one duration, @ 24.677 is 39000ms on 77.1 and 4 others,
  @ 21.570 is 30000ms, and the first tick after an application is 1.191s at the median over 362 (claim: 361).

### U5. "The 2 Mara fights with the sigil"

- **Place:** `NOTES.md:353-355`, commit `f6c66619`.
- **Evidence:** three Mara players have Inspired Allegiance events with themselves as source: CwmX2Tb8GA6hyc1v fight 8
  (74 events), w7khnMKAa4TxXrmC fight 23 (38) and R2Bxhvkf3rc8FH4X fight 1 (49). The first two also have another
  player's on them. The 678 baseline issues and none at the head hold, and so do the times 20.204 to 28.219 and 22.205 to
  30.223. The split of 496 and 182, and "496 of 995 checks", were not recounted.

### U6. The copies' tick period entry has numbers from before the last commit

- **Place:** `NOTES.md:469-479`.
- **Evidence:** "against the engine's 13,465, 2,217 and 616": the head has 13,741 hits matched, 2,493 Hemorrhage matched,
  and 253 engine-only with 121 missing (374 in the test's column). The 0.631 holds: median 0.632, p10 0.621, p90 0.646
  over 1,610 pairs by this session's stricter pairing (claim: 2,227 pairs, 0.635, 0.620, 0.721). A refresh outside
  Maiden of Death is 0.724 at the median over 76 pairs (0.709 to 0.752), against 0.87 / 1.2 = 0.725. Game data: the
  listener order of abilities 1517 and 1589 is the registration order, which the game data does not give; 0.63075 and
  0.725 are its two outcomes. The 13,768, 2,520 and 379 with scaler 0.631 need an engine change and were not rerun.

### U7. The free dice entry's seed 1 numbers

- **Place:** `NOTES.md:480-489`, commit `8f21b87c`.
- **Evidence:** T2qLPpmMkrAtJ9B1 fight 18, seed 1, at the head, engine / log: Arachnid Assault 89.5% (claim 86.5%),
  Queen's Fang 88.8% (88.2%), Vexira's Venom 88.8% (87.1%), Amethyst Splinter 91.1% (88.1%), Skittering Blades 101.6% and
  Volatile Poison 101.7% (98.7% to 99.0%); Arachnid Assault's crit rate 83.0% against the log's 83.4% (claim 81.5%). The
  finishers below the generators holds. Pass-03's 92.8% and 97.1% hold. The 175 casts forced with no Combo Point and the
  129 short of Energy are in no output of a free dice run of `MaraReplayTests` and were not recounted.

### U8. "Every value has its game data source in the code comment"

- **Place:** pull request body; `ConjurePortal.cs:13-14`, `Rejuvenate.cs:12`.
- **Evidence:** `ConjurePortal.CooldownSeconds`, `ConjurePortal.GlobalCooldown` and `Rejuvenate.CooldownSeconds` have
  "Game data (build 26320967)" in the class comment and no record, row or asset property. The sources this session's
  runs gave: `RelicPower.Portal.Cooldown` (120); `GE_GlobalCooldown` `Coefficient` 1.5 with `GEC_HasteMagnitude` 1 / Haste,
  Haste snapshot at the cast; `RelicPower.HealthPotion.Cooldown` (120).

## Values checked

No numeric literal of the diff equals a log / engine ratio of `mara-bleeds.baseline.json`, its inverse, or a product of
either with a known multiplier: the relic abilities have no damage ID, and Hemorrhage's 1.064 and 1.053 match none of
0.87, 1 / 1.2 and 0.725.

| Value in the diff | Game data, this session's runs | Logs, this session's recount |
| --- | --- | --- |
| Bloodrite Fervor cooldown 90 | `RelicPower.PartyMoveSpeedBuff.Cooldown` 90 (`RelicPowers.csv`, `AbilityConstants` registry) | 20 casts; shortest gap 90.148s (b649dhNptyW132Tm fight 5 @ 517.886) |
| Bloodrite Fervor: no global cooldown | No `GlobalCooldownGameplayEffectClass` on the class defaults; check passes on a null class (0x1450DC801); the constructor's default is unread | A global cooldown ability 0.129s to 0.335s before 3 casts and 0.368s to 0.703s after 9 |
| Bloodrite Fervor buff: effect 156, 12s, MoveSpeed x1.5, 4000 units | `GE_RelicPower_PartyMoveSpeedBuff_Buff`; rows `.Duration` 12, `.MoveSpeedScaler` 1.5, `.MaxRange` 4000 | - |
| Relic cooldown gem factor 1 - 0.08, 1 - 0.24 | `GMMC_GemAugmentation.CalculateBaseMagnitude` offsets 516 to 1237; `CT_GemConstants` rows Small 0.08, Medium 0.24; Diamond nodes 5 (450) and 10 (1500); the Strength requirement is in the deprecated field and unenforced. Node 10 against node 5: unread | None: every caster has Diamond power 0 (U2) |
| No Haste or Cooldown Recovery on a relic cooldown | `GE_Shared_Cooldown_RelicScale` has asset tag `EffectLogic.System` alone | - |
| Conjure Portal cooldown 120 | `RelicPower.Portal.Cooldown` 120, times the gem factor (L3) | 10 casts; two in one fight twice, 672.650s and 148.463s apart |
| Conjure Portal global cooldown 1.5, Haste | `GE_GlobalCooldown` `Coefficient` 1.5, `GEC_HasteMagnitude` 1 / Haste, snapshot | Next global cooldown cast 1.139s (Hwxc8TRFqPGhfjbN fight 5 @ 446.832) and 1.438s (HgamNcX24tJ3qVQA fight 21 @ 691.259) after |
| Padding cooldown 0.3 | `Hero.AbilityPaddingCooldown.Default.Duration` 0.3; its Haste rule is unread | - |
| Portal Sickness: effect 2176, 20s, no modifier | `GE_RelicPower_Portal_RecentlyUsedPortal`, `RelicPower.Portal.PortalSicknessDebuff.Duration` 20, applied by ability 1506 | - |
| Relic Teleport: no cooldown, no global cooldown | No `CooldownDuration`, no `GlobalCooldownGameplayEffectClass`, `BlockedByPaddingCooldown` false; the constructor's default class is unread | 26 casts; 0.636s and 0.804s after Conjure Portal (Zta3LQdvVbPJ9Whz fight 6, GXFz3pZ94aQr2chV fight 3); 0.733s and 0.898s after a global cooldown ability (v8rhNxVc3PwDqzBA fight 4 p27 @ 678.493, w7khnMKAa4TxXrmC fight 23 @ 704.934). Forced casts in those four fights: 6 at pass-03, 2 at the head |
| Rejuvenate cooldown 120 | `RelicPower.HealthPotion.Cooldown` 120, times the gem factor (L3) | 2 casts, 346.804s apart (WcTBrQNX46m9axqP fight 12 @ 278.213 and 625.017) |
| Rejuvenate: no global cooldown | No class on the class defaults; tooltip "Can be used during Global Cooldown" | None: neither cast is within 1.2s of another cast |
| Rejuvenate heal 0.4 of maximum health, no spread | `RelicPower.HealthPotion.MaxHealthFraction` 0.4 on effect 160; preset `Empty` (`DefaultSpread` 0, `CanCrit` false) | - |
| From the Shadows: period scaler x 0.87 on each application | Ability 1589 offsets 127 to 376, listener at 2164, row `...CloneAttack.TickPeriodMultiplier` 0.87; written on a refresh too | fjrCax7VzGmMZw6h fight 26 alone: 0.724 median after a refresh outside Maiden of Death (76 pairs) |
| Bloodrush: period scaler x 1 / 1.2 after the application | `GA_Mara_BleedAttackSpender` offsets 1876 to 2209, row `...IncreasedTickRateWhileSingle.TickRateIncrease` 1.2; gate is the owner's talent tag, with no target count | Same 76 pairs |
| The scaler starts at 1 on a refresh | The refresh assigns the new spec (0x144A6A660, call at 0x144A6A93B) and the ability's spec has no override; the getter's value for a spec with none (0x1452365A0) is unread | 0.724 after a refresh, against 0.725 without compounding |
| The running tick keeps its fraction | 0x1452DC7E0 through 0x1452F2A70 | - |
| `DamageOverTime` on effects 1724, 1716, 2337 | `EffectType.Dot` on all three; none of `TickrateNotAffectedByHaste`, `...Exclusively`, `IgnorePartialTicks` | Tick time issues: Hemorrhage 2,606, Seething Poison 1,180 (M2) |
| No partial tick on a removal | The one decoded caller of the partial tick routine is `CheckDuration` (0x144A6FD3A); the removal paths are unread | - |
| Gushing Blood: 4 others, those without the bleed first, then the struck enemy | `MaxNumberOfTargets` 4, radius 600, angle 270, `OrderType` `Closest`, struck enemy appended last; the preference rule is native and unread | 0 to 4 others: 41, 1, 11, 11, 51 of 115 (L1) |
| Gushing Blood's gate, removals and copies | Ability 1517: gate on Maiden of Death's asset tag (2755), removal 2558, clone 2592 to 2684, per target removal 1406 and application 1600, Bloodrush's write 1864 to 2197 | 115 of 115 in Maiden of Death; every copy one duration; 39000ms on a refresh (the carry-over is in the copies) |
| Copies start at scaler 1 (0.725) | Unread order of two listeners: 0.63075 or 0.725 | 0.632 (U6) |
| `Math.Max(Blocked, Mitigated)` | - | M1, U3 |
| `Def.SourceId == 0` for the hero's own aura | - | CwmX2Tb8GA6hyc1v fight 8: another player's 20.204 to 28.219, the Mara player's 22.205 to 30.223; 678 issues at the baseline, 0 at the head |

## Numbers

```
ACCURACY_HERO=Mara ACCURACY_LABEL=review ACCURACY_FREEDICE_ITERATIONS=100 dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests
```

| Measure | Baseline | Review | Pull request body |
| --- | --- | --- | --- |
| Hits matched | 99.5% (536,191 of 538,809) | 99.7% (537,348) | 99.5% → 99.7% |
| Exact replay damage / log | 99.9% | 99.8% | 99.9% → 99.8% |
| Free dice mean / log, 100 runs | 92.8% | 93.2% | 92.8% → 93.2% |
| Mean log percentile in free dice | 96.4% | 96.0% | 96.4% → 96.0% |
| fjrCax7VzGmMZw6h fight 26 hits matched | 89.1% | 96.7% | 89.1% → 96.7% |
| fjrCax7VzGmMZw6h fight 26 exact replay | 90.0% | 98.1% | 90.0% → 98.1% |
| fjrCax7VzGmMZw6h fight 26 free dice / log | 122.0% | 144.8% | - |
| gd6ByNrf81jc9aWw fight 3 exact replay | 104.1% | 99.9% | 104.1% → 99.9% |
| Unmodeled casts | 32 | 0 | 32 → 0 |
| Inspired Allegiance issues | 678 | 0 | 678 → 0 |
| Hemorrhage matched of 6,117 | 4,982 | 5,989 | 5,666 → 5,989 (W1) |
| Hemorrhage engine-only | 274 | 284 | 757 → 412 (W1) |
| Hemorrhage calibration, normal and crit | 1.064, 1.053 | 1.090, 1.077 | - |

Every fight's exact replay is 99% to 101% but fjrCax7VzGmMZw6h fight 26 (98.13%).

Tests: `dotnet test tests/FellowshipSim.Tests -c Release` has 1,604 passed, 10 skipped, 0 failed, the pull request body's
count. `dotnet test -c Release` on the solution stops on this machine before any test, with NETSDK1147 (workload
`maui-tizen`) for `FellowshipSim.Desktop`, a project the branch leaves unchanged; `tests/FellowshipSim.Tests` is the
solution's one test project.

No line the branch adds, no commit message and no sentence of the pull request body names another hero.
