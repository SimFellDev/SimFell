# Ardeos tests: where they came from and where they went

Sources: `fellowship_elarion_sim/tests/functional/ardeos` (Python, game season 2) and `fellowsimc/tests/cpp/ardeos` (C++, season 3). The C# engine
targets season 4; where a spec disagrees with a known season 4 value, the season 4 value is tested (see `test_description.md`). There are no Ardeos
integration or unit tests in either project. Classes are `Ardeos<File>`.

| Source | Test | Ported to |
|---|---|---|
| py `test_complex` | Searing Blaze spam, untalented: no damage, no Cinders | not ported (season 2 rule; since season 3 a recast leaves the ticks alone) |
| | Searing Blaze spam with Agonizing Blaze: ticks (Cinders check dropped: per-tick Cinders unsettled) | `AbilitiesComplex` (20% haste row skipped) |
| py `test_damage` | Detonate + Wildfire +20% | `AbilitiesComplex` (season 4: +30%) |
| | Detonate scaling with haste, Expertise, Intellect | `AbilitiesSimple` (ratios; the crit rows dropped: the Python dice never crit) |
| | Detonate + Devouring Flame boots +8% | `AbilitiesComplex` (season 4: +7%) |
| | Detonate only hits enemies with an effect | `AbilitiesSimple` |
| | Searing Blaze, Engulfing Flames, Fire Ball burn scaling (6 sets) | `AbilitiesSimple` (ratios, no absolute bases; the crit row dropped) |
| | Fire Frogs burn scaling | not ported (needs a single frog attack) |
| py `test_timings` | Detonate always 1s | `CastTimes` (fails: engine 0.967s from logs) |
| | Incinerate downtime and hits | `CastTimes`, `AbilitiesComplex` |
| | burn tick counts: Engulfing Flames, Incinerate | `HasteBreakpoints` |
| | Searing Blaze (24s) | `HasteBreakpoints` (season 4: 30s, 15/17/18 ticks) |
| | Fire Ball burn hasted | not ported (unhasted since season 4; the C++ test is ported) |
| | Fire Frogs and Crackling Inferno burns | not ported (single frog attack; needs a critical Infernal Wave) |
| C++ `test_ardeos_fireball` | 3 (6 equal ticks, recast 7 ticks, Wildfire 8 ticks) | `Specific/FireBallDot` (the last two were disabled in C++; active here) |
| C++ `test_ardeos_smoke` | smoke cast | not ported (trivial) |
| | Engulfing Flames ticks per cast count | `AbilitiesComplex` |
| | recast leaves Searing Blaze's rhythm alone | `AbilitiesComplex` |
| | full Searing Blaze: 12 Cinders | `ResourceEconomy` (skip: per-tick Cinders unsettled) |
| | 30 real casts (with and without Agonizing Blaze) | `AbilitiesComplex` (the plain one skipped: it assumes 1 Cinder per tick) |
| | Engulfing Flames durations (6 tests) | `Specific/EngulfingFlamesDuration` |

Added from the engine's documented values (the "other" tests, to fill the categories): `ResourceEconomy` (30 Cinders, Embers, Detonate's cost),
`CastTimes` (Engulfing Flames, instants), `CooldownDurationSimple` (charges and cooldowns), Fire Ball's landing delay in `Specific/FireBallDot`.
