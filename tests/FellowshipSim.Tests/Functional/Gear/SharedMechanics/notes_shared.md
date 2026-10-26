### Shared mechanics (Gear/SharedMechanics)

Hero-independent rules from `Hero.cs`, run over all six heroes (`GearHero.All`): Spirit Refund, Rising Spirit, the Spirit ability and Spirit of Heroism, passive Spirit Points, cooldown recovery, Covetous Greed. 143 test cases (141 pass, 2 skipped, none failing).

Failing on purpose: none.

Skipped: the buff from Overlord Varux in Scryer's Peak (a damage multiplier; not found in the engine or game data) and the debuff some enemies apply to themselves that makes them take more damage (separate effect, not in the engine). Each is a skipped test with a blurb and a TODO in `SharedUnfoundEffects`.

Followed the engine, with a TODO in the test: Rising Spirit's +2 seconds on a further application (measured from 10 Rime logs, not game data); Spirit Refund's 0.2s delay (the tests only check the resource is back after 0.5s).

Observation (not tested, for review): after casting an ability while a cooldown-recovery effect is up, `GetCooldownRemains` reads the full base cooldown and then drops faster than the rate (Ice Blitz 60 -> 27.5 in 5s at 2x); the cooldown tests therefore time when the ability can be cast again, which matches cooldown / rate.

Not covered: Spirit Points earned from damage (enemies' Spirit Points, `CreatureSpiritPoints`), Spirit ability cost reduction (Blessing of the Prophet; with the gems), Spirit of Heroism's companion auras and its staying through death, haste's effect on cooldowns (hero-specific), cooldown acceleration by glyphs and weapons (their own folders), Gunde's feather side of the refund (a Gunde topic), two Covetous Greed relics at once.
