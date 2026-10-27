# Elarion tests: where they came from and where they went

Sources: `fellowship_elarion_sim/tests` (Python: functional, integration, unit) and `fellowsimc/tests/cpp/elarion` (C++).
Classes are `Elarion<File>`; "skip" means skipped with a TODO (spec and engine disagree, value to check in game).

| Source | Tests | Ported to |
|---|---|---|
| py functional `test_cast_times` | 7 cast times, rotation | `CastTimes` |
| | Supremacy breakpoints | `HasteBreakpoints` |
| | EH volley-reset breakpoints | `Specific/EventHorizonCooldown` (skip) |
| py functional `test_cdr` | 5 (HWA+EH, chrono+EH x2, skylit x2) | `Specific/EventHorizonCooldown` (skylit with 1.0 per volley: skip; 1.2 version active) |
| py functional `test_focus` | 11 net costs | `ResourceEconomy` |
| py functional `test_spirit_proc` | Spirit regen with Spirit | `ResourceEconomy` |
| | spirit proc gives +1 Spirit Point | `AbilitiesComplex` (Spirit Refund) |
| | enemy-based regen, Willful Momentum | not ported (Python scenario machinery; Sigil) |
| py functional `test_mark_proc` | CS from CI / spirit proc, proc probabilities | `Specific/LunarlightMark` |
| | Amethyst Splinters / Kindle DoTs | not ported (generic gem/sigil) |
| py functional `test_legendaries` | neck (4), cloak (2), boots (3) | `Specific/Legendaries` (boots, FC+cloak: skip) |
| py functional `test_gems`, `red_gem_1`, `entity_kill`, `main_stat_calculation`, `scenarios`, `crit_rates` (3 statistical) | | not ported: generic gems/weapon traits, Python setup and scenario machinery. Crit chances per talent are in `TalentsSimple` instead |
| py integration `damage_scaling` | 14 | `AbilitiesSimple` (stat sets; HWA 70% ricochet: skip) |
| py integration `target_count` | 8 | `AbilitiesSimple` (hard caps of 12 dropped, soft falloff tested) |
| py integration `focus_economy` | 13 | `ResourceEconomy` (EH rounding of Celestial Shot's cost: skip) |
| py integration `ability_interactions` | multishot charges (skip), supremacies, RW, EH, FC (skip), Lethal Shots, Shimmer | `AbilitiesComplex`, `TalentsComplex`, `TalentsSimple`, `AbilitiesSimple`, `Specific/Legendaries` |
| py integration `cooldown_reduction` | Skylit (skip), Skyward, Repeating Stars (skip) | `CooldownDurationSimple`, `Specific/EventHorizonCooldown` |
| py integration `lunarlight` | chances, stacks, no recursion, explosion (skip), Fury, Affinity (skip) | `Specific/LunarlightMark` |
| py integration `proc_mechanics` | Celestial Impetus, spirit proc, Starstriker's | `AbilitiesComplex`, `Specific/Legendaries` |
| py integration `timing` | Volley/Barrage ticks, Impending, Volley stacking | `HasteBreakpoints`, `TalentsSimple`, `AbilitiesComplex` |
| py unit (base classes, talent budget) | | not ported (Python internals) |
| C++ `test_focus_economy` | 5 | `ResourceEconomy` |
| C++ `test_timings` | hits breakpoints (3) | `HasteBreakpoints` |
| | HWA cooldown, +EH, +legendary recovery, +chrono, Grace with volleys | `Specific/EventHorizonCooldown` |
