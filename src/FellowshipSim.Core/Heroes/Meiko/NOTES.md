# Meiko: what the logs showed

How Meiko's mechanics were pinned down, beyond the tooltips. Each point is also a comment next to the code it shapes.
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
- **Shatter Earth:** the slam lands 0.27-0.37s after the cast; the quake ticks every hasted second from there.
- **Twin Souls: Army of One:** the Sibling of Wind strikes one enemy at the cast and every 0.25s after, not hasted, for
  20s: 80 strikes, not the tooltip's 40 (7,649 strikes in 98 casts).
- **Auto attacks:** a 1.9s swing shortened by Haste.

## Spirit Refunds

- A Spirit Refund comes on Stormfall alone: 544 of 2,232 Stormfall consumptions came back 0.2s later with Rising Spirit,
  and none of 958 Earthfall consumptions, though hero 13's Spirit text names both. The engine's Spirit / (1 + Spirit)
  expects 552.
- Each refund grants Spirit Armor and a stone for Stone Shield (7Zmfx9gq6HjvThzc fight 2 @ 25.482: 2 stones to 3).

## Replay matches

| Fight | Hits matched | Damage of log |
|---|---|---|
| 2NwV9b6jdhFnzXJk fight 2 | 97.0% | 99.0% |
| 3M2vWr8taxVfTQFY fight 53 | 97.9% | 98.9% |
| 7Zmfx9gq6HjvThzc fight 1 | 77.0% | 94.2% |
| 7Zmfx9gq6HjvThzc fight 2 | 75.8% | 94.0% |
| BWFTRPJkz24QLjxM fight 2 | 76.8% | 93.5% |
| DYL3Ph6Fkgp8jVbw fight 1 | 98.0% | 100.9% |
| fXyzwGcQkTrLD34R fight 4 | 87.7% | 96.6% |
| GXFz3pZ94aQr2chV fight 3 | 72.8% | 94.2% |
| gZdryvDb9th8XxqM fight 5 | 72.7% | 93.4% |
| Hzc78X3TFPvMVnhb fight 3 | 76.2% | 94.3% |
| kYCNMwGW6cRKngr2 fight 2 | 73.6% | 93.4% |
| P1yMmDGAWhnHNXTQ fight 16 | 74.8% | 95.0% |
| PFtrX9RZJxmv1QfT fight 2 | 78.8% | 95.5% |
| PrW4639VM7hYFD2g fight 13 | 73.3% | 93.7% |
| VTghN4MvYn73PW9k fight 1 | 76.1% | 93.0% |
| xPntaRwjmCWcqVfg fight 3 | 75.9% | 93.2% |

The fights below 97% hits matched are the players with Al'zerac's Shackle, a weapon the engine does not have (its
54,746 Slow hits, 4,207 hits and 2,350 Freeze hits are 5% of the logs' damage).

## Known gaps

- **Al'zerac's Shackle** (weapon ability 152) is not in the engine.
- **The quake** ticks on every enemy at once; the game ticks each enemy on its own timer from the moment it stands in
  the area. 3,282 of its 21,392 logged ticks find no engine tick (89% of its damage in an exact replay).
- **Free dice** come in at 82% of the logs' damage. The players time Rising Storm and Lashing Stormkick to the Stormfall
  their refunds gave back; with the refunds at random, a free-dice run consumes 18% fewer Stormfall charges on the casts
  of 7Zmfx9gq6HjvThzc fight 2 (79 against 96), with less Rising Spirit, Warden of the Temple and Double Palm Strike.
- **Tank mechanics:** damage taken, armor, dodge, parry, the stones' health, healing and the Sibling of Earth's health are
  not simulated. Stone Guard and Shattering Obelisk's pulses come from them, and Stone Shield's stones stay until the
  next cast.

## Not yet checked against a log

Will of Stone, Everlasting Spirit, Slipstream, Peacefield and Earthbourne's 20% are built from their tooltips and the
game data (marked `UNVERIFIED` in their files). Forbidden Technique and Tectonic Impact are in 3M2vWr8taxVfTQFY fight 53
alone.
