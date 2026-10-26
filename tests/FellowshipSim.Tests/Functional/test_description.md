# Functional tests

Check the sim against the game's rules: one behaviour per test, each readable as a player scenario ("casting Frost Bolt gives 3 Anima").

**Tests are normative.** A failure means a possible engine bug or a changed rule. Never bend a test to match the engine: if the rule is disputed or unverified, skip it with a reason.

## How they run

Step-by-step mode (`StepDriver`): the test, not an action list, controls the hero.

- `Cast<T>(target?)` casts and returns once the hero is free again (cast, channel and global cooldown over). It throws if the cast is refused; `TryCast` returns the refusal instead.
- `Wait(seconds)` lets time pass. The fight clock is `Now`.
- State is read straight off the hero (resources, auras, cooldowns). `Events` is the ordered timeline: casts, damage (with target and crit chance), resource gains, spends and overflows, auras.
- `StepFights.Rime(...)`, `StepFights.Ardeos(...)`, `StepFights.Mara(...)`, `StepFights.Gunde(...)`, `StepFights.Tariq(...)` and `StepFights.Elarion(haste, enemies, talents, crit, spirit, mainStat, expertise, weaponAbilityId, legendaries)` build a bare hero: no gear, and 0 crit and 0 Spirit unless asked. Damage is the expected value, so crit-free ratios are exact.
- `ForceProcs` decides random procs (including Spirit Refund) so tests never depend on dice; it also records the chance the engine gave each proc (`Rolls`), which is how proc chances are tested.

## Conventions

- Every test has a doc comment: `Player:` the rule in game terms, `Dev:` how it is done (set-up shortcuts, wait lengths, tolerances).
- Game values are literals in the test, not engine constants.
- Times are whole milliseconds: tolerance 1–2ms, damage ratios 0.001. Avoid hastes that land exactly on a breakpoint unless that is the point.
- Variants of one rule (e.g. the torrent talents) share an abstract base tester with one nested class per variant.
- Disputed or unchecked rules: `Skip = "reason"` plus a TODO in the comment. Say what to check ("check in game").
- Don't assert on random outcomes; force them.

## Layout

One folder per hero (`Rime/`, `Elarion/`, `Ardeos/`, `Mara/`, `Gunde/`, `Tariq/`), the same files in each (a hero without tests for a category has no file for it; Elarion has no `CooldownDurationComplex`, its combined-cooldown tests being in `Specific/EventHorizonCooldown`). Classes carry the hero's name when it is not Rime (`ElarionCastTimes`) since all files share one namespace. `Generic/` tests the harness itself.

| File | Covers |
|---|---|
| `ResourceEconomy` | builders, spenders, caps, passive regen, scaling with targets (simple checks) |
| `CastTimes` | down time of each ability at several haste values |
| `AbilitiesSimple` | one ability on its own: damage, targeting, area falloff, charges, buffs |
| `AbilitiesComplex` | abilities together: swallows, buff stacking, Wrath of Winter interactions, Spirit Refund |
| `TalentsSimple` | one talent on its own |
| `TalentsComplex` | talents together or with several abilities |
| `CooldownDurationSimple` | base cooldowns, haste, reductions from one effect |
| `CooldownDurationComplex` | reductions from several effects, free casts, refunds |
| `HasteBreakpoints` | hits per dot or channel against the formulas |
| `Specific/` | hero-only topics (Ardeos: Fire Ball's burn, Engulfing Flames' duration; Rime: Winter's Embrace timing and haste windows; Elarion: Event Horizon and cooldown acceleration, Lunarlight Mark, legendaries; Mara, Gunde and Tariq: none yet, see the topics listed in each `test_inventory.md`) |

To add a hero: same files, same categories, a bare-hero builder in `StepFights`.

### Gear (`Gear/`)

Hero-independent effects, tested over all six heroes. `GearFights.cs` has a row per hero (`GearHero`: one ability of each category, and a `Fight(GearLoadout)` builder taking glyphs, augmentations, weapon, relics, gem power), and gear tests are `[Theory]`s over `GearHero.All` comparing a hero with and without the effect (ratios, so no hero numbers). Weapons run over the hero's legal set (melee: Fated Strike, Sahril's Wrath, Voidbringer's Touch, Earthbreaker; ranged: Chronoshift, Icicles of An'zhyr, Voidbringer's Touch, Nature's Fury). One folder per category (`SharedMechanics/`, `Glyphs/`, `Aspects/`, `Sigils/`, `Weapons/`, `Relics/`, `Gems/`, `Smoke/` for the fixture itself), each with a `notes_<category>.md` holding its full failing/skipped list, and `Complex/` for effects together. Each category has its own namespace (`GearGlyph`, `GearSigil`...) and class prefix since their helpers share method names. Out of scope: stat enchants, legendaries (a `Legendaries.cs` per hero is planned), Frigid Winds.

## Open

Every question the tests leave open, in one place, so they can be settled in game or with the other contributors. Each is also in the TODO of its test. A test is either **failing** (a possible engine bug, or a rule changed), **skipped** (the rule itself is not settled), or the area is **not covered**.

### Rime
- **Failing:** Cold Snap's charge returns late with haste (the faster rate starts after the first global cooldown).
- **Skipped, to settle with the other contributors:** torrent partial ticks and Coalescing Frost falloff (both changed in the SimFellDev merge); Soulfrost grievous crits (game bug); Biting Cold crit formulas; Glacial Assault explosion count; Avalanche and Cold Shower interactions; swallows still in flight.
- **Not covered:** passive Spirit regen; Spirit Refund rate; gem, glyph and weapon cooldown sources (the green-gem rank 10 TODO); Harrowing Ice at 31 stacks from real pulses.

### Elarion, Ardeos, Mara, Gunde, Tariq
Listed in each hero's section below.

### Elarion

Ported from the Python sim (`fellowship_elarion_sim`) and the C++ tests (`fellowsimc`); see `Elarion/test_inventory.md` for what went where and what was not ported. Where the Python/C++ spec and the engine disagree the test follows the spec: failing when the engine side is unverified, skipped with a TODO when the engine's value comes from the game data or combat logs. Failing on purpose: Event Horizon and Chronoshift/legendary recovery multiplying, Skylit Grace per volley, Event Horizon's global cooldown, Starfall Volley's tick count and haste snapshot, the Skystrider's Supremacy breakpoints at 12.4% and 87.4% haste, Spirit regeneration with Spirit, Impending Heartseeker's first arrow.

### Ardeos

Ported from the Python sim (season 2) and the C++ tests (season 3); see `Ardeos/test_inventory.md`. The C# engine targets season 4, so where a spec disagrees with a known season 4 value the season 4 value is used. Season 4 values used: Searing Blaze lasts 30 seconds (15 ticks) and a recast leaves its tick rhythm alone (so spamming it deals damage); Wildfire is +30% tick speed; Devouring Flame is +7% per Engulfing Flames; Searing Blaze and Engulfing Flames can crit while Fire Ball's and Fire Frogs' burns cannot (without a talent); Fire Ball's burn is not hasted; Detonate's cast time is 1 second. Crit is not tested on Ardeos: the Python sim's crit rows never roll a crit (its dice are fixed).

Damage over time ticks are read with `Ticks(effectId)` and direct hits with `Hits(ability)` (`Ardeos/Common.cs`; a damage event carries the ability's ID).

Failing on purpose: Detonate's cast time (the engine has 0.967s from combat logs, the game 1s), Fire Ball's recharge with haste (late by about a global cooldown, as Rime's Cold Snap), and two Slow Burn durations (11.52s against 11.0s, 12.2s against 12.5s). Fire Ball's flight (0.97s, fixed) is in the sim, and setting it to 1.0s changes neither result, so it is not the cause: the first is likely a tick landing exactly on the end of the effect (the 5th tick at 11.0s extends it in the engine, not in the game).

Skipped, to check in game: Cinders per Searing Blaze tick (the engine has 0.8, believed to be 1), which covers the full-burn and thirty-casts tests, and Agonizing Blaze spam at 20% haste (44 ticks against 45, on the edge of the fight's end).

### Mara

No tests for her exist in the Python sim or the C++ tests, so these were written from the engine's code, the tooltips quoted in it and `Mara/NOTES.md`; see `Mara/test_inventory.md` for what is covered and the topics left for `Specific/`. The rules follow the tooltips; values the engine takes from combat logs (Energy regeneration, partial ticks, Volatile Poison's tick count) are tested as the logs showed them.

Failing on purpose: Widow's Bite's recharge with haste (the charge comes back about 0.17s late: same bug as Rime's Cold Snap and Ardeos's Fire Ball).

Skipped, to check in game: Backstab's 40% from behind (the engine always applies it), and whether Gushing Blood's +50% and Seething Poison's +20% on the bleed multiply (1.8) or add (1.7).

Not covered: the extension of Matriarch Macabre by a second Macabre Stratagem (a 180 second cooldown, no reset), Energy regeneration from Venomous Delight at the cap, and the specific topics in `Mara/test_inventory.md`.

### Gunde

No tests for him exist in the Python sim or the C++ tests, so these were written from the engine's code, the tooltips quoted in it and `Gunde/NOTES.md`; see `Gunde/test_inventory.md` for what is covered and the topics left for `Specific/`. Values the engine takes from combat logs (Serrated Edge's 8 seconds, Oathshatter's falloff beyond 8, Owed in Blood's 0.85 per stack beyond 15, the 0.5s feather pickup) are tested as the logs showed them.

Failing on purpose: a fight starts with one stack of Owed in Blood in the engine (the tests expect none); Grim Carve's third spin comes at 1.10s in the engine and 1.14-1.21s in the logs; Bloodbath does not shorten Reign in Blood's running cooldown (ready at 90s, expected 84s).

Skipped, to check in game: the chance of a Blood Feather per Rend tick (the engine's 15% is an estimate from one log), and whether Butcher's Hook applies Rend (the game data text has a 50% placeholder, no log shows it).

Not covered: Bloodbound Spirit's global cooldown, ranged Double Strike, passive Spirit regeneration (a Spirit Point every 3 seconds in a fight), and the specific topics in `Gunde/test_inventory.md`.

### Tariq

No tests for him exist in the Python sim or the C++ tests, so these were written from the engine's code, the tooltips quoted in it and `Tariq/NOTES.md`; see `Tariq/test_inventory.md` for what is covered and the topics left for `Specific/`. Values the engine takes from combat logs (Face Breaker's 50% splash, Sledgehammer's 10%, Hammer Storm's 1.35 ramp per spin multiplying, Skull Crusher's shortened global cooldown, Chain Lightning's and Leap Smash's timings) are tested as the logs showed them; where a log and the game data disagree the log wins and the test says so.

Failing on purpose: Kill Em All's +60% cooldown acceleration on Heavy Strike (the cooldown is over after 5.56 seconds instead of 5: the acceleration stops when the cast ends although a stack is still up).

Skipped, to check in game: Wild Swing's avoid chance (the engine's 7% is an estimate from three logs), whether the free Chain Lightning of Blood & Thunder and Ace of Spades needs Thunder Call (the engine says no, unverified), and whether Ace of Spades rolls for each thunder bolt and each Hammer Storm spin.

Followed the engine, with a TODO in the test: whether the "enemies hit" counts of the falloffs (Wild Swing beyond 5, Face Breaker, Heavy Strike's cleave and Sledgehammer beyond 12 and 8) include the target (the engine counts the enemies each part hits); whether The Motherload's bonus counts the new enemy itself (the engine's second hit already has +65%); Ride the Lightning's 30 Fury (the test accepts 30 to 31.7: the engine's ticks follow haste including the talent's own 5%); Fury draining out of combat (Bloodline's tooltip says it stops; the engine never drains); Thunder Call being blocked during Raging Tempest (from a guide and 45 logs).

Not covered: the legendaries (Executioner's Grin, Thundering Vortex, Slayer's Mosh) and Sahril's Arms, grievous crits, passive Spirit regeneration, Bloodline's movement speed, and the specific topics in `Tariq/test_inventory.md`.

### Gear (generic effects)

Written from the engine's code, the tooltips quoted in it and the combat-log notes in it; see `Gear/*/notes_*.md` for each category's full lists.

Failing on purpose:
- **Gilded Broach:** the engine has none of its +5% main stat (12 cases; assumes it multiplies like other main-stat buffs).
- **Gems:** Ancient's Wisdom counts twice on a live power change (1249 to 1250); Might of the Minotaur not applied on a live rise from 79 to 80; Blessing of the Commander and Blessing of the Prophet do not follow a live power change; a hero built from gem power gets no overcap bonus.
- **Weapons:** none now (Voidbringer's Touch's cap eruption scales with Expertise, which is correct; the engine's comment saying otherwise is stale).
- **Aspects:** Amethyst Splinters drops the tick landing exactly 8.0s after the crit (3 ticks instead of 4 with no haste); Brave Machinations with Nature's Fury cuts the cooldown twice per cast (the first hit lands before the cast completes, resetting the once-per-cast flag).
- **Sigils:** Kindling rolls on a hero's own damage-over-time ticks (Ardeos's Fire Ball burn, Gunde's Rend); the game data says ticks do not roll.
- **Glyphs:** The Mystic does nothing to Ardeos's Detonate (its damage skips the ability multipliers).

Skipped, to check in game: Chronoshift's own cooldown recovery during its channel (suspected sim mistake); Voidbringer's Touch's cooldown shortened by haste (every other weapon ability's is fixed); the engine letting any weapon go on any hero; Earthbreaker (missing from the engine); Hunter's Focus's classification of "targeted offensive" (the game may count only successful targeted casts); Kindling's tooltip total (7 ticks against 6); Patient Soul's movement; the buff from Overlord Varux in Scryer's Peak and enemies' self-applied damage-taken debuff (not found in the engine, one blurb test each); The Sinister on Tariq's Heavy Strike; The Vehement counting Gunde's two-hit Double Strike once or twice; The Subduer's Control cooldown reduction; flat-rating gem nodes on a hero built from gem power; Might of the Minotaur's health condition and Blessing of the Conqueror's boss condition (no health or boss model).

Followed the engine, with a TODO in the test: class damage tuning on aspect, sigil and weapon damage (Kindling's burn, Emerald's spike, Diamond's beam, Voidbringer's eruption twice); "increased" main stat bonuses adding (Power Revealed + Vengeful Soul) while Rising Spirit multiplies; Rising Spirit's +2s per further application (from Rime logs); The Philosopher's refresh carrying 30%; The Vehement repeating a hit that already has The Subduer's bonus; Fated Strike's cleave count; which abilities Blessing of the Commander cuts; whether Diamond Strike and Emerald Judgement roll on ticks, and which abilities roll Ruby Storm; whether Sapphire Aurastone takes hits at a pulse's own instant.

Not covered: stat enchants, legendaries, healing, shield and armor nodes, defensive enchants beyond a no-effect check, the barrier's damage reduction, Icicles's curse snapshot rules, Nature's Fury's bounce targeting, Martial Initiative's damage reduction, Spirit Points earned from damage, two Covetous Greed relics at once, Earthbreaker.

Engine note: `GetCooldownRemains` and cooldowns that start with a recovery effect or haste already up run their first interval at rate 1 (a new `CooldownState` never reads its rate at the first `Spend`); this is likely the cause of the known late-recharge failures (Cold Snap, Fire Ball, Widow's Bite) and is not fixed. Tests wait a second before reading such cooldowns.
