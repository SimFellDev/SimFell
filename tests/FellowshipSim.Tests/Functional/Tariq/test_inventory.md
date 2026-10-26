# Tariq test inventory

No Tariq tests exist in the Python sim (`fellowship_elarion_sim`) or the C++ tests (`fellowsimc`), so nothing was ported. The tests were written from the engine's code (`Heroes/Tariq/`: the tooltips quoted in each ability and talent, `NOTES.md`), one behaviour per test, rules from the tooltips and values from the logs as the notes give them. They are to be reviewed against the game. Hits are compared with a Wild Swing hit on one enemy (129.14% of Strength) as the unit, as Mara's are with Skittering Blades and Gunde's with an auto attack. Hits that carry their own crit chance (Leap Smash) are divided by what that chance is worth in expected value (`TariqFights.CritMultiplier`).

## Files

| File | Covers |
|---|---|
| `ResourceEconomy` | Fury (start at 0, cap 100, no drain), the builders (Wild Swing 3, Face Breaker 7, Heavy Strike 12, Chain Lightning 1.2 a hit), the spenders (Skull Crusher 25, Hammer Storm 50, Culling Strike up to 10), Focused Wrath's discount (52%, 2 stacks, 15s), Spirit Refund on a spend (Fury back, Rising Spirit, a Spirit Point, the roll) |
| `CastTimes` | the 1.5s hasted global cooldown, Skull Crusher's shortened 1s one, the off-global abilities (Leap Smash, Culling Strike, Thunder Call, Focused Wrath, Raging Tempest), a rotation, haste changing with Raging Tempest |
| `AbilitiesSimple` | damage coefficients, Wild Swing (no crit, crit chance as damage, avoided hits, falloff beyond 5), Face Breaker splash (50%, no crit, falloff beyond 12), Heavy Strike cleave (falloff beyond 8), Skull Crusher single target, Hammer Storm (3 spins, ramp 1.35, times, falloff beyond 12), Culling Strike (Execution at 30%, 40% a Fury), Chain Lightning (needs Thunder Call, 6 hits, times, targets), Leap Smash (0.3s, falloff beyond 8, own crit chance), Thunder Call's lightning on each ability, Focused Wrath's damage, Raging Tempest (Spirit cost, leap, haste, charge, Thunder Call swap, stacks, Expertise, Raging Currents) |
| `AbilitiesComplex` | the order of a Thunder Call Heavy Strike and Hammer Storm spin, Hammer Storm's lightning falloff beyond 15, a Skull Crusher at another target, Focused Wrath over a whole Hammer Storm and on a thunder bolt, Rising Spirit, refunds of discounted and Culling Strike Fury, Heavy Strike lightning in Raging Tempest, Raging Currents on random enemies |
| `TalentsSimple` | all 18 talents, one each (Left Hand Path, Ride the Lightning, Square Hammer, High Road, Blood & Thunder, Bloodline, The Motherload, Mouth for War, Thunderstruck, Pneuma, Sledgehammer, Far Beyond Driven, Kill Em All, Ace of Spades, Schism, Them Bones, Crack the Sky, Killing in the Name) |
| `TalentsComplex` | High Road with Mouth for War, Focused Wrath's 3-stack cap, Mouth for War's stack making Skull Crusher cheaper, Focused Wrath with Schism (2.75), Schism over Hammer Storm's spins, Sledgehammer with boosts, Thunderstruck with The Motherload and with Ace of Spades, everything a Heavy Strike triggers, Bloodline with Ride the Lightning |
| `CooldownDurationSimple` | base cooldowns, haste on the hasted ones (Face Breaker 6, Heavy Strike 8, Culling Strike 6, Chain Lightning 8, Leap Smash 20; Wild Swing, Skull Crusher and Hammer Storm ready with the global cooldown), fixed ones (Thunder Call 45, Focused Wrath 90), Raging Tempest with none |
| `HasteBreakpoints` | spins and bounces are not hasted, Raging Currents strikes and Bloodline Fury are |

There is no `CooldownDurationComplex` (the talents that cut cooldowns are in `TalentsSimple`: High Road, Square Hammer, Kill Em All, Ace of Spades).

## Topics left for `Specific/` (not tested; for review)

- **Legendaries and the weapon:** Executioner's Grin (6% on every cast, the weapon's too; a free Culling Strike on any target as if 30 Fury were spent, the effect's own text says 20); Thundering Vortex (Chain Lightning +50% Fury, a stack per bounce, 100% more damage at 20 stacks, the effect's own text says 60%); Slayer's Mosh (enemies hit by Leap Smash take 20% more from you for 6s); Sahril's Arms (the weapon ability).
- **Grievous crits:** Them Bones' +100% crit chance on top of crit rating beyond 100% (a grievous crit); the engine caps the crit chance reported at 100%.
- **Hammer Storm in detail:** the spin times in server frames (0.033, 0.402 and 0.801s at 29% haste), a channel cut short stopping the later spins, spins keeping what the cast had (Focused Wrath, Schism) after Thunder Call ends.
- **Chain Lightning's target choice:** hitting enemies not yet hit first, the replay following the log.
- **Raging Tempest and Thunder Call:** Ride the Lightning's haste and Fury during Raging Tempest, Thunder Call blocked during it (from a guide and 45 logs), the Tempest's Enhanced Thunder Call opening Chain Lightning.
- **Face Breaker's splash against Left Hand Path's text:** the game data says 25% of a crit's damage, the logs 50% of every hit.
- **Skull Crusher's placeholder 1.2** (`Talent.IncreasedDamageScaler`): left out because the logs fit without it.
- **Spirit Points from damage** (the shared formula runs high, see `NOTES.md`) and the passive Spirit Point every 3 seconds.

## Not tested and why

- Passive Spirit regeneration (a Spirit Point every 3s shows in the fights; the Pneuma test reads points as a difference to be free of it): no rule to check against.
- Bloodline's +20% movement speed: no movement in the step driver.
- Wild Swing's avoid chance (7%): an estimate from three logs, skipped.
- Leap Smash with a landing in the replay (a cast at an empty spot) is tested only through High Road's cooldown reduction.
