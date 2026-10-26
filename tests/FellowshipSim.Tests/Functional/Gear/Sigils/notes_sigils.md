# Sigil tests: notes

All 8 sigils, every rank, every hero (`GearHero.All`), written from the code's tooltips and the sigil classes. Files: `Sigils/Sigil*.cs` (one per sigil, `SigilCommon.cs` the helpers) and `Complex/SigilComplex.cs`. 498 tests (493 passed, 3 skipped, 2 failing on purpose).

## Failing on purpose (possible engine bug)
- **Kindling rolls on other damage-over-time ticks (Ardeos, Gunde).** The game data says a tick does not roll (listener `InTriggerPeriodicExecutions` false). The engine skips only the ticks registered in `Hero.DotDamageIds`, and only gear effects (Kindling, Amethyst Splinters, Icicles' curse) register there, so Fire Ball's burn and Rend ticks roll: 7 rolls instead of 1 for Ardeos, 6 instead of 2 for Gunde. Test: `SigilKindling.Ticks_Do_Not_Roll`. The other four heroes pass (their unit hit has no damage over time).

## Skipped (to check in game)
- **Hunter's Focus: what counts as a targeted offensive ability** (`SigilHuntersFocus.Only_Targeted_Offensive_Abilities_Count`). The engine counts any ability with a damage coefficient, at cast completion. The game is thought to count successful casts of targeted abilities only; area abilities, effect ticks and abilities that pick their own target are unsettled. The stack, duration, rank and target-switch tests run.
- **Kindling's total** (`The_Burn_Deals_The_Tooltips_Total`): the tooltip's 483% at rank 1 is 7 ticks of 0.69, the engine has 6 ticks (9s at 1.5s); how many ticks the game's burn has is not settled. The other Kindling tests follow the engine's 6.
- **Patient Soul's "3 seconds still / 6 seconds after moving"**: movement is not simulated.

## Followed the engine, with a TODO in the test
- **Increased Main Stat bonuses add** (Power Revealed +7.2% with Vengeful Soul +3.0% = +10.2%, not 1.0722 x 1.03): taken from `Hero.PrimaryAttribute`'s comment, no game check.
- **Rising Spirit and Willful Momentum multiply** (1000 x 1.05 x 1.015): same source.
- **The class damage tuning scales Kindling's burn** (Rime +4%, Ardeos +10%, Elarion +8%, Mara +4%, Gunde and Tariq none); the tests multiply by it from a literal table. Whether the game applies it to a sigil's burn is unchecked.
- **Aura expiry**: an aura is removed up to a server frame (17ms) after its time; durations are tested within 50ms.
- **Inspired Allegiance's weapon cooldown** is compared between two identical fights with the proc forced in one; tolerance 0.15s because a Void-touched Conduit (Voidbringer's Touch) cooldown recovers with haste.
- **Wild Swing never crits in the engine** (`canCrit: false`), so the crit sigils (Seized Opportunity, Vengeful Soul) use Face Breaker for Tariq. Not checked against the game.

## Not covered
- Patient Soul's +5% Max Health (defensive).
- Vengeful Soul's heal below 50% health (defensive).
- Inspired Allegiance's allies (the allies' share is a party buff handled by the replay).
- Sigils staying through the hero's death (Hidden Power, Seized Opportunity, Inspired Allegiance: `KeepsOnDeath` from logs); the step fight has no death.
- The Vehement-style combinations with glyphs, and sigils together with gems or aspects beyond the Main Stat sum.
- Kindling's partial last tick at other hastes than 20%, and Kindling with a Spirit Refund.
- Hunter's Focus at ranks above 1 with five stacks (rating 80 passes the 10% diminishing-returns breakpoint).
