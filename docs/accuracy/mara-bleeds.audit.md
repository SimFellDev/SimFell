# Spec audit: mara-bleeds

Hero: Mara. Branch `mara-bleeds` at `ea159e9b`. Spec `docs/accuracy/mara-bleeds.md` at `5b15d856`. Baseline: `docs/accuracy/mara-bleeds.baseline.txt` and `.json`, identical to `data/accuracy/Mara/baseline.*`.

Audit measure: `ACCURACY_HERO=Mara ACCURACY_LABEL=audit ACCURACY_FREEDICE_ITERATIONS=100 dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests`. It passed and wrote `data/accuracy/Mara/audit.txt`.

The measure's issue list stops at 40 subjects. The issue counts for A4, A5 and A6 below are full counts. They come from an exact replay of the same 34 Mara players with every issue counted. That replay's 983 `Energy too low` equals the measure's.

**Fight set.** The measure has the 34 fights of the spec and no others. All 34 match on report, fight and player name, and each row's `data/fights/<report>_fight<fight>_p<player>.raw.json` exists. No fight is missing and none is added.

## Items

| Item | Verdict | Target | Baseline | Audit | Commit or NOTES.md line |
| --- | --- | --- | --- | --- | --- |
| A1. Bloodrite Fervor (185) | Not met | modeled from game data | 20 unmodeled casts | 0 unmodeled casts | `4af892c6`, `58105f15`, `e3d54716`; NOTES.md:220-225, 497-501 |
| A2. Conjure Portal (1504) | Not met | modeled from game data | 10 unmodeled casts | 0 unmodeled casts | `c50e5cd2`; NOTES.md:226-228 |
| A3. Rejuvenate (189) | Not met | modeled from game data | 2 unmodeled casts | 0 unmodeled casts | `d78039b4`; NOTES.md:234-236 |
| A4. Trait : Focused Haste (`Aura:1002141`) | Not met | 0 aura issues | 1,177 wrong stacks (top-40 list) | 1,177 wrong stacks, 34 extra in engine | none |
| A5. Energy (`Resource:Energy`) | Not met | 0 resource issues (tolerance 3.0) | 1,039 too low (top-40 list) | 983 too low, 28 too high, in 29 fights | NOTES.md:449-454 (23 fights, before the branch) |
| A6. Inspired Allegiance (`Aura:1002112`) | Met | 0 aura issues | 678 extra in engine | 0 issues of any kind | `f6c66619`; NOTES.md:348-355; `ReplayVerifier.cs:324` |
| A7. fjrCax7VzGmMZw6h fight 26, Leknok (23) | Not met | exact replay / log 99% to 101% | 90.0% (hits 89.1%) | 98.1% (hits 96.7%) | `f815f3fa`, `a0c63984`; NOTES.md:141-167, 469-479 |
| A8. gd6ByNrf81jc9aWw fight 3, sbt (4) | Not met | exact replay / log 99% to 101% | 104.1% | 99.9% | `35956983`; NOTES.md:54-62; `ReplayScript.cs:155-160` |

**A1.** The numbers meet the target, but two values name no source:
- **Cooldown Recovery flag.** `BloodriteFervor.cs:38` sets `IgnoresCooldownRecovery = true`. Its comment is "Game data: no Haste and no Cooldown Recovery reaches a relic cooldown", which names no record, asset, function or address.
- **Gem factor at Diamond power 1500.** At 1500 the factor is 1 - 0.24:
  - `Gem.Nodes` holds the highest five nodes, and a rank II node replaces the rank I node of its slot (`Gem.cs:32`).
  - `NodeInSlot(5)` (`Gem.cs:191`, with `Slot` at `Gem.cs:20`) is therefore node 10, read at `BloodriteFervor.cs:60`.
  - NOTES.md:498-501 says the cooldown "reads node 5 alone (0.08 at 450)", which contradicts the code.
  - The same entry says it is not decoded whether node 10's effect replaces node 5's. The 0.24 therefore has no source.
- **Sourced values.** The cooldown of 90, the 12s buff and the MoveSpeed 1.5 each name a `CT_RelicPowers` row.

**A2.** `ConjurePortal.cs:13` (`CooldownSeconds` 120), `:14` (`GlobalCooldown` 1.5), `:19-20` (the global cooldown and its Haste scaling) and `:22` (`IgnoresCooldownRecovery`) name no source. The class comment gives only "Game data (build 26320967)". The review (L3) reports `GE_Shared_Cooldown_RelicScale` with the relic cooldown gem factor on this cooldown. The engine has no such factor here, and this audit did not re-read the game data.

**A3.** `Rejuvenate.cs:12` (`CooldownSeconds` 120), `:20` (`IgnoresGcd`) and `:22` (`IgnoresCooldownRecovery`) name no source. `MaxHealthFraction` 0.4 names its row. The review's L3 on the relic gem factor applies to this cooldown too.

**A4.** Where the issues are:
- 3M2vWr8taxVfTQFY fight 53: 588 wrong stacks and 14 extra in engine.
- Prvc4XmbdHBaVRKy fight 12: 589 wrong stacks and 20 extra in engine.

No commit changes Focused Haste, and "Not yet explained" has no entry for it. NOTES.md:272-274 counts its engine changes against log events, but that count is in the Energy section.

**A5.** The "Energy gains with no proof" entry (NOTES.md:449-454) gives 679 too low and 33 too high over 23 fights. Those are not the fight set's final numbers (983 and 28 over 34 fights). No commit changes Energy.

**A6.** All 34 fights have 0 Inspired Allegiance issues. `IsOwn` (`ReplayVerifier.cs:324`) names its log pattern: CwmX2Tb8GA6hyc1v fight 8 has two instances, and NOTES gives counts for 2 fights (496 and 182).

Finding: NOTES.md:353 says "the 2 Mara fights of `data/fights` with the sigil". The review (U5) counts 3 Mara players with their own Inspired Allegiance; the third is R2Bxhvkf3rc8FH4X fight 1. That fight has 0 issues in the audit replay. This audit did not recount the sigil.

**A7.** The fight is off target. Also:
- **Stale "Not yet explained" entry.** NOTES.md:469-479 gives numbers from before `a0c63984`: 13,465 hits matched, 2,217 and 616. The audit has 13,741 hits matched (96.7%). The entry has no exact replay figure, so it is not an Explained gap.
- **Copy scaler with no source.** The engine starts Gushing Blood's copies at scaler 1 (0.725). `Hemorrhage.cs:88-90` and NOTES.md:472-479 both call the order of the two listeners unread, so that value has no source.
- **Gushing Blood targets.** `GushingBlood.cs:29` takes up to 4 other enemies with no radius or angle. NOTES.md:165 gives 62 of 114 strikes with no other enemy. The review (L1) recounts 41 of 115 and gives the game data's radius of 600 and angle of 270.
- **Free dice.** Fight 26's free dice is 144.8% of the log in the audit, against 122.0% at the baseline. It has no line in NOTES. The review (L2) traces the rise to From the Shadows' rolls.
- **From the Shadows source.** `FromTheShadows.cs:6-15` drops `UNVERIFIED` and states that the clone's Queen's Fang is logged as ability 1201, with no log source. NOTES.md:493-496 still lists From the Shadows as unverified and asks how its clone is logged.
- **Counts that differ from the audit measure.** NOTES.md:146-147 and commit `a0c63984` give Hemorrhage as "5,666 to 5,989 of 6,117, engine-only 757 to 412" and Seething Poison as "9,906 to 9,931 of 9,933 and 187 to 153". The measure gives:
  - Hemorrhage: 4,982 to 5,989 of 6,117 matched, engine-only 274 to 284.
  - Seething Poison: 9,982 to 10,007 of 10,009 matched, engine-only 160 to 151.

**A8.** The numbers meet the target, but the change's value does not meet the spec's Evidence clause:
- **How the target was reached.** `UnblockedAmount` (`ReplayScript.cs:160`) is `TotalAmount + Math.Max(Blocked, Mitigated)`. It is the log side of every exact replay ratio (`ReplayReportWriter.DamageTotals`, `ReplayReportWriter.cs:61-62`):
  - Fight 3's log damage goes from 47,208,806 to 49,169,284.
  - Its engine damage goes from 49,136,508 to 49,139,446.
  - The 104.1% to 99.9% therefore comes from the larger log total.
- **One fight.** The comment at `ReplayScript.cs:155-159` gives a log pattern from gd6ByNrf81jc9aWw fight 3 alone. NOTES.md:58 names 3tM2NRFDCnhkqdx6 fight 7 with 185 hits and no ratio. The clause asks for at least two fights.
- **`Math.Max`.** The choice of `Math.Max` names no source.
- **The pattern fails for Greed's Embrace.** Effect 649 has `IncomingDamageMultiplier` 0.2 in `PartyBuffs.cs:81-82`. The engine's hit on such an enemy is already the reduced one, and the mitigated part added back counts the reduction twice.
- **Consistent drops in three fights.** From baseline to audit, za2QymCwxMcqfH16 fight 20 goes from 99.4% to 99.1%, CwmX2Tb8GA6hyc1v fight 8 from 99.9% to 99.6%, and pWmcV42K39GHTLQX fight 23 from 99.8% to 99.6%. This is consistent with the double count; other commits sit between the two measures.
- **Review figures, not recounted.** The review's M1 gives 242 Greed's Embrace hits at 4.477x the engine. It gives Arachnid Assault and Queen's Fang at 0.918 and 0.922 of amount plus mitigated in the fight 3 windows.

## Changes outside the spec

Traced hunks:
- A1: `BloodriteFervor.cs`, `CommonAbilities.cs:13`, NOTES.md:220-225 and 497-501.
- A2: `ConjurePortal.cs`, `CommonAbilities.cs:14`, NOTES.md:226-228.
- A3: `Rejuvenate.cs`, `CommonAbilities.cs:15`, NOTES.md:234-236.
- A6: `ReplayVerifier.cs`, NOTES.md:348-355.
- A7: `Hemorrhage.cs`, `HemorrhagingStrike.cs`, `MaraDot.cs`, `FromTheShadows.cs`, `Bloodrush.cs`, `GushingBlood.cs`, `MaraTalentSmokeTests.cs:306`, NOTES.md:141-167, 469-479 and 493.
- A8: `ReplayScript.cs`, `RawEvent.cs`, `ReplayScriptBuilder.cs:531`, `LogOutcomeSource.cs`, `ReplayDriver.cs:283`, `ExactReplayAssertions.cs:13-14`, NOTES.md:54-62.

Hunks with no item:

| Commit | File and lines | Change |
| --- | --- | --- |
| `0c2ba780` | `tests/FellowshipSim.Tests/MaraReplayTests.cs:18-19, 22, 26-27, 68-69, 155-157, 163, 165-172` | `MARA_FIGHTS` filter, `MARA_REVIEWS` review pages, forced casts by reason, hit dump columns |
| `8f21b87c` | `tests/FellowshipSim.Tests/MaraReplayTests.cs:38-52, 54-63` | `MARA_FREEDICE_SEED` free-dice run and its tally per damage ID |
| `8f21b87c` | `src/FellowshipSim.Core/Heroes/Mara/NOTES.md:480-489` | "Free dice below the log": the headline free-dice target, no item. It gives pass-03's 92.8% and 97.1%; the audit has 93.2% and 96.0% |
| `ac96c059` | `src/FellowshipSim.Core/Customization/CommonAbilities/RelicTeleport.cs:4-10, 17-20`; NOTES.md:229-233 | Relic Teleport (ability 1506) waits for no global cooldown. It is not on the baseline's unmodeled list; A2 is ability 1504 |
| `a0c63984` | `src/FellowshipSim.Core/Heroes/Mara/Abilities/SeethingPoison.cs:27-29`; `src/FellowshipSim.Core/Heroes/Mara/Talents/Hemotoxin.cs:58-63` | Effects 1716 and 2337 on the period timer; their numbers are over the 34 fights, with none for an item |
| `a0c63984` | `tests/FellowshipSim.Tests/MonteCarloSeedTests.cs:96-97` | Mara's pinned totals repinned. They follow the `MaraDot` change, which covers A7's Hemorrhage hunks and the Seething Poison and Hemotoxin hunks above, so the repin has no single item |
| `ea159e9b` | `docs/accuracy/mara-bleeds.review.md:1-305` | Stage 3 adversarial review (`docs/REPLAY_ACCURACY.md`) |

No added line and no commit message on the branch names a hero other than Mara.

## Spec changes

None. `git log origin/main..mara-bleeds -- docs/accuracy/mara-bleeds.md` lists `5b15d856` alone. The spec and both baseline files are unchanged since that commit.

## Headline targets

`35956983` adds the mitigated part to the log side of the exact replay. Exact replay log damage goes from 1,618,017,899 at the baseline to 1,621,427,165 in the audit. Free-dice log damage is 1,615,978,731 in both. The exact replay numbers below therefore have a larger denominator than the baseline's. 1% of the audit's log damage is 16.21M, and 8 damage IDs are over it.

| Measure | Spec target | Baseline | Audit | Result |
| --- | --- | --- | --- | --- |
| Exact replay damage / log, each fight | 99% to 101% | fjrCax7VzGmMZw6h fight 26 90.0%, gd6ByNrf81jc9aWw fight 3 104.1% outside | fjrCax7VzGmMZw6h fight 26 98.1% outside; the other 33 are 99.1% to 100.6% | Not met |
| Free dice mean / log, all fights | 98% to 102% | 92.8% | 93.2% | Not met |
| Mean log percentile in free dice | 30% to 70% | 96.4% | 96.0% | Not met |
| Calibration, normal and crit, damage IDs over 1% | 0.98 to 1.02 | all 8 in range | Arachnid Assault 1.000/1.005, Queen's Fang 0.987/1.003, Vexira's Venom 1.002, Amethyst Splinter 1.001, Volatile Poison 1001731 1.008/1.009, Skittering Blades 1.000/1.002, Sahril's Wrath 1.005/1.006, Volatile Poison 1001730 1.006/1.006 | Met |
| Log hits matched | 98% or more | 99.5% | 99.7% (537,348 of 538,809) | Met |
| Engine-only hits, damage IDs over 1% | 2% of log hits or fewer | at most 0.90% (Queen's Fang) | at most 0.91% (Queen's Fang, 105 of 11,597) | Met |

## Checks

| Check | Spec value | Audit |
| --- | --- | --- |
| `dotnet test -c Release` | passes | Not met as run here. It stops before any test with `error NETSDK1147: To build this project, the following workloads must be installed: maui-tizen` for `src/FellowshipSim.Desktop/FellowshipSim.Desktop.csproj`, a project the branch leaves unchanged. `dotnet test tests/FellowshipSim.Tests -c Release`: 1,604 passed, 10 skipped, 0 failed. |
