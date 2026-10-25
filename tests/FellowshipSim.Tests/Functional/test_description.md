# Functional tests

Check the sim against the game's rules: one behaviour per test, each readable as a player scenario ("casting Frost Bolt gives 3 Anima").

**Tests are normative.** A failure means a possible engine bug or a changed rule. Never bend a test to match the engine: if the rule is disputed or unverified, skip it with a reason.

## How they run

Step-by-step mode (`StepDriver`): the test, not an action list, controls the hero.

- `Cast<T>(target?)` casts and returns once the hero is free again (cast, channel and global cooldown over). It throws if the cast is refused; `TryCast` returns the refusal instead.
- `Wait(seconds)` lets time pass. The fight clock is `Now`.
- State is read straight off the hero (resources, auras, cooldowns). `Events` is the ordered timeline: casts, damage (with target and crit chance), resource gains, spends and overflows, auras.
- `StepFights.Rime(haste, enemies, talents, crit, spirit)` builds a bare hero: no gear, and 0 crit and 0 Spirit unless asked. Damage is the expected value, so crit-free ratios are exact.
- `ForceProcs` decides random procs (including Spirit Refund) so tests never depend on dice.

## Conventions

- Every test has a doc comment: `Player:` the rule in game terms, `Dev:` how it is done (set-up shortcuts, wait lengths, tolerances).
- Game values are literals in the test, not engine constants.
- Times are whole milliseconds: tolerance 1–2ms, damage ratios 0.001. Avoid hastes that land exactly on a breakpoint unless that is the point.
- Variants of one rule (e.g. the torrent talents) share an abstract base tester with one nested class per variant.
- Disputed or unchecked rules: `Skip = "reason"` plus a TODO in the comment. Say what to check ("check in game").
- Don't assert on random outcomes; force them.

## Layout

One folder per hero (`Rime/`), the same files in each. `Generic/` tests the harness itself.

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
| `Specific/` | hero-only topics (Rime: Winter's Embrace timing and haste windows) |

To add a hero: same files, same categories, a bare-hero builder in `StepFights`.

## Open

- **Failing:** Cold Snap's charge returns late with haste (the faster rate starts after the first global cooldown).
- **Skipped, to settle with the other contributors:** torrent partial ticks and Coalescing Frost falloff (both changed in the SimFellDev merge); Soulfrost grievous crits (game bug); Biting Cold crit formulas; Glacial Assault explosion count; Avalanche and Cold Shower interactions; swallows still in flight.
- **Not covered:** passive Spirit regen; Spirit Refund rate; gem, glyph and weapon cooldown sources (the green-gem rank 10 TODO); Harrowing Ice at 31 stacks from real pulses.
