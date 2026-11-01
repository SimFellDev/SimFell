# Meiko: what the logs showed

Meiko's mechanics as the combat logs showed them, beyond the tooltips. Each point is also a comment next to its code.
Measured on the 16 Meiko players of patch 0.5.0.0 in the 15 fights `tools/fetch-fights.cs --hero Meiko` downloaded
(October 2026) unless noted. Game values are from game data build 26320967.

## Combos

- **Finishers:** the first of the last two Combo Builders picks the technique, the second the finisher. Of 5,309
  finishers after 2 builders (the 27 downloaded fights with a Meiko): Wind Kick then Earth Fist is Lashing Stormkick
  (2,095), Wind Kick then Spirit Palm Rising Storm (856), Earth Fist then Wind Kick Earthfist Barrage (898), Earth Fist
  then Spirit Palm Rising Earth (397), Spirit Palm then Wind Kick Double Palm Strike (552), Spirit Palm then Earth Fist
  Spirited Vortex (511). After 3 builders in a row, the last two count.
- **Stone Stomp** takes no combo, though the game data gives it a cost of 2 CP: 54 of its 111 casts have no builder
  before them.
- **Energy:** the builders cost 30 of a resource whose regeneration the game data does not give. The logs report no such
  resource, and their builders come every global cooldown in runs of up to 46, so the engine leaves the cost out.

## Damage

- **Damage IDs:** Stone Shield's stones are logged as effect 1572, Shatter Earth's quake as effect 1556, the Sibling of
  Wind as effect 1548, Stone Stomp as effect 1542 and Forbidden Technique's eruption as effect 1509. Every hit of
  Earthfist Barrage, the uppercut too, is logged under 1112. Perfect Storm's extra kick is ability 2310.
- **Conclusive Strikes** does not raise Earthfist Barrage, though the game data's changes list names it: Barrage hits
  came in at 0.85-0.95 of the engine's with the 10% for the players with the talent, and 1.00-1.01 for those without it.
  The other finishers match within 1.2% with it.
- **Perfect Storm:** the extra kick strikes every enemy the kick struck, 0.40s later, at 1.01x the kick's hit at the
  median, each hit with a crit roll of its own. A chance of 25% x (1 + Haste) gives 725 procs expected against 738
  logged.
- **Tectonic Impact:** the uppercut's explosion is logged under 1112 on the other enemies, each hit with a crit roll of
  its own, sqrt(1/N) of the uppercut (3M2vWr8taxVfTQFY fight 53 @ 391.801: 1,821 on each of 4 enemies).
- **Earthen Skin** is the 20% on Wind Kick, Rising Storm, Lashing Stormkick, the Sibling of Wind, Shatter Earth (slam and
  quake), Spirited Vortex and Stone Shield, all fight long. Rumbling Stone's 20% covers Shatter Earth's slam but not the
  quake (the game data's changes lists).

## Timing

- **Global cooldown:** 1s, not hasted (8,862 of the 11,816 gaps under 3s between builders and Shatter Earth are
  1.03-1.07s). Finishers, Stone Shield, Serenity and the Spirit ability are off it.
- **Earthfist Barrage:** no cooldown, though the game data gives it 45s: it is cast 2.4-11s apart (7Zmfx9gq6HjvThzc
  fight 2 @ 237.301, 239.672, 242.543). Its 5 hits come 0.04, 0.27, 0.54, 0.77 and 1.01s after the cast at every Haste,
  and the next builder comes 1.0s or more after it.
- **Stone Shield:** 5 hits on every enemy, 0.04, 0.27, 0.50, 0.77 and 1.00s after the cast, though the tooltip says 4.
- **Spirited Vortex:** a hit on every enemy at the cast, then every hasted 2s for 30s.
- **Shatter Earth:** the slam is 0.27-0.37s after the cast; the quake ticks every hasted second from there.
- **Twin Souls: Army of One:** the Sibling of Wind strikes one enemy at the cast and every 0.25s after, not hasted, for
  20s: 80 strikes, not the tooltip's 40 (7,649 strikes in 98 casts).
- **Auto attacks:** a 1.9s swing shortened by Haste.

## Spirit Refunds

- A Spirit Refund comes on both charges. 544 of 2,232 Stormfall consumptions had Rising Spirit at the consumption and
  the Stormfall back 0.2s later; the engine's Spirit / (1 + Spirit) expects 552. Earthfist Barrage consumes Earthfall at
  the cast, and its refund is at the channel's end: 189 of 937 Earthfall consumptions had Rising Spirit 1.0s after the
  cast and the Earthfall back at 1.2s.

## Replay matches

| Fight | Hits matched | Damage of log |
|---|---|---|
| 2NwV9b6jdhFnzXJk fight 2 | 97.6% | 99.8% |
| 3M2vWr8taxVfTQFY fight 53 | 99.0% | 100.6% |
| 7Zmfx9gq6HjvThzc fight 1 | 95.4% | 99.1% |
| 7Zmfx9gq6HjvThzc fight 2 | 96.9% | 100.2% |
| BWFTRPJkz24QLjxM fight 2 | 97.3% | 99.5% |
| DYL3Ph6Fkgp8jVbw fight 1 | 98.3% | 100.6% |
| fXyzwGcQkTrLD34R fight 4 | 95.7% | 97.8% |
| GXFz3pZ94aQr2chV fight 3 | 96.5% | 101.2% |
| gZdryvDb9th8XxqM fight 5 | 95.5% | 99.3% |
| Hzc78X3TFPvMVnhb fight 3 | 96.7% | 99.4% |
| kYCNMwGW6cRKngr2 fight 2 | 96.7% | 99.5% |
| P1yMmDGAWhnHNXTQ fight 16 | 97.0% | 100.9% |
| PFtrX9RZJxmv1QfT fight 2 | 96.5% | 99.8% |
| PrW4639VM7hYFD2g fight 13 | 96.9% | 100.4% |
| VTghN4MvYn73PW9k fight 1 | 96.7% | 99.0% |
| xPntaRwjmCWcqVfg fight 3 | 96.3% | 99.0% |

Al'zerac's Shackle is the largest source of unmatched hits: 6,761 of its 54,746 Slow ticks find no engine tick.

## Known gaps

- **The quake** has a timer of its own on each enemy the slam struck. An enemy in the area after the slam has no quake
  ticks in the engine: 2,326 of the 21,392 logged ticks find no engine tick (92.6% of its damage in an exact replay).
- **Free dice** come in at 90.4% of the logs' damage, from free-dice variance in the game's own mechanics. The players
  time Rising Storm and Lashing Stormkick to the Stormfall their refunds gave back. With the refunds at random on the logged casts, a free-dice run consumes 12% fewer
  Stormfall charges (1,942 against 2,208 over 16 fights), and Warden of the Temple enhances 21% fewer Double Palm
  Strikes (315 against 397). The exact replay's Spirit Refunds are 733 of 3,145 rolls against 769 expected. Lashing
  Stormkick (84.6%) and Spirited Vortex (87.6%) have most of the gap. In 5 fights Meiko has Storm's Fury (effect 2699,
  +20% Critical Strike chance for 20 seconds, from Storm Shield: Lightning Strike), and the log's crits are 4.6-5.7
  points above the engine's crit chance on every ability. Al'zerac's Shackle: Slow's log/engine ratio is 0.974
  with Twin Souls: Army of One up and 1.070 without it.
- **Damage alone:** the engine simulates the damage Meiko deals, not damage taken, mitigation or healing. Shattering
  Obelisk's pulses come from damage the Sibling of Earth takes, so they are a defensive proc: a replay deals one at each
  pulse hit in the log, and a sim never. No log has the talent.

## Not yet checked against a log

Will of Stone and Everlasting Spirit are built from their tooltips and the game data (marked `UNVERIFIED` in their
files). Forbidden Technique and Tectonic Impact are in 3M2vWr8taxVfTQFY fight 53 alone. There, a Double Palm Strike
while Forbidden Technique is active erupts the active one at once (33 times), and 545 of its 549 eruption hits match.
