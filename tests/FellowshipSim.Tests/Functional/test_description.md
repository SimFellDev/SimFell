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

One folder per hero (`Rime/`, `Elarion/`, `Ardeos/`, `Mara/`, `Gunde/`, `Tariq/`), the same files in each (a hero without tests for a category has no file for it; Elarion has no `CooldownDurationComplex`, its combined-cooldown tests being in `Specific/EventHorizonCooldown`). Classes carry the hero's name when it is not Rime (`ElarionCastTimes`) since all files share one namespace. `Generic/` holds the hero-independent tests of the harness and shared rules: `StepDriverTests` (the harness itself) and `SpiritPointRegeneration` (a flat Spirit Point every 3 seconds for all six heroes, whatever the Spirit stat).

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
| `Specific/` | hero-only topics (Ardeos: Fire Ball's burn, Engulfing Flames' duration, every burn's tick speed with haste and Wildfire (`DotTickSpeed`); Rime: Winter's Embrace timing and haste windows; Elarion: Event Horizon and cooldown acceleration, Lunarlight Mark, legendaries; Mara, Gunde and Tariq: none yet (`Mara/Specific/` is empty), see the topics listed in each `test_inventory.md`) |

To add a hero: same files, same categories, a bare-hero builder in `StepFights`.

### Gear (`Gear/`)

Hero-independent effects, tested over all six heroes. `GearFights.cs` has a row per hero (`GearHero`: one ability of each category, and a `Fight(GearLoadout)` builder taking glyphs, augmentations, weapon, relics, gem power), and gear tests are `[Theory]`s over `GearHero.All` comparing a hero with and without the effect (ratios, so no hero numbers). Weapons (and the aspects that depend on the weapon) run over each hero's own list of authorized weapons (`GearHero.WeaponAbilities`: a list per hero, no grouping of heroes by role or main stat). One folder per category (`SharedMechanics/`, `Glyphs/`, `Aspects/`, `Sigils/`, `Weapons/`, `Relics/`, `Gems/`, `Smoke/` for the fixture itself), each with a `notes_<category>.md` holding its full failing/skipped list, and `Complex/` for effects together. Each category has its own namespace (`GearGlyph`, `GearSigil`...) and class prefix since their helpers share method names. Out of scope: stat enchants, legendaries (a `Legendaries.cs` per hero is planned), Frigid Winds.
