# Ardeos: what the logs showed

These are Ardeos's mechanics as the combat logs show them, in the places the tooltips leave open. Each point is also a
comment next to the code it shapes.

The logs are 5 Ardeos fights (yMAZVJL9hTpFjqbC fight 7 on 2026-09-05; 1p7AXmHfwV9CYMxR fight 30, dBgbn8wD1RxL7Kma
fight 9, Fc8v6yWxD14GQMnV fight 19 and YC861NTRgLbmw4Qa fight 31 on 2026-10-22/23). Each point names its fights.

## Damage over time

- **Tick rate:** Searing Blaze, Engulfing Flames and Incinerate tick faster with haste. Fire Ball's and Fire Frogs'
  effects tick every 2s and 3s at any haste. Wildfire makes every effect tick 1.3 times as fast, and the ticks re-time
  the moment the rate changes (fight 7).
- **Fire Ball and Fire Frogs effects:** a new hit adds its share to the effect: the ticks left times their damage, plus
  the new share, over the full duration's base ticks (6 and 4). The ticks keep their timing (fight 7: Fire Ball on 19.4 @
  73.66s gives (50.5 + 473) / 6 = 87 a tick, as logged). The ticks left count at the base rate. In Wildfire, Fire Frogs
  ticks fit that count within 12%, and a count at Wildfire's rate would multiply the damage with every frog.
- **Incinerate's extension** (1.5s a hit) never leaves an effect more than its full duration (fight 7: Fire Ball on 19.4
  after 8 hits @ 42.08-47.45s).
- **Slow Burn:** 0.5s per Fire Ball tick at any tick rate (fight 7: an Engulfing Flames of 12s on 25.1 lasts 15.5s through
  7 Fire Ball ticks, some in Wildfire).
- **Incinerate's stacks:** a tick at 8 stacks is 3.1 times one at 1 stack: stack n deals 1 + 0.3 (n - 1) times the base
  (fight 7).
- **Cinders:** a tick at a faster rate generates less, so an effect generates the same Cinders per second: Engulfing
  Flames ticks give 3.85 Cinders, and 2.94 in Wildfire, 3.85 / 1.3 (fight 7). Fire Ball ticks generate 2 times the
  falloff of the Fire Ball that applied them (deltas of 1.17-1.43). The tooltip gives this a 50% chance, but replays
  of all 5 fights refuse the fewest Detonates for want of Cinders when every tick generates.

## Detonate

- The cast is instant. Each enemy with an effect takes 3 hits, about 0.3s apart, and each hit is the damage its effects
  deal per second at the cast. Hits that land later keep that amount (fight 7 on 29.2 @ 220.6-225.1s: the hits include
  an Incinerate effect that had ended). The hits crit on their own, and the effects keep ticking.
- The hits take no multiplier of their own: the effects' damage already has every damage bonus. The logged Detonate
  damage over the replay's is 0.98-1.07 of the same ratio for the effects it detonates (five fights, mean 1.03).
- Detonate's global cooldown is 29 server frames (0.967s), not hasted. Across the five fights, 896 Detonates in a row
  come 0.96-0.97s apart at every haste from 0.2 to 1.3. The other instants take the hero's 1.5s global cooldown, hasted.
- On many enemies the server sends the hits one frame at a time, so they queue up and land up to several seconds late
  (fight 7: 89 hits in the 5.1s after the last cast of a pull).

## Fire Ball, Infernal Wave, Apocalypse

- Fire Ball strikes 0.95-1.0s after the cast (fight 7, 40 casts). Infernal Wave lands 0.05-0.65s after it.
  Apocalypse lands as its cast completes.

## Fire Frogs

- A cast's frogs make their first attacks 1.7-2.6s after it. A lone frog attacks every 20 server frames (fight 7, final
  boss 32.1 @ 683.0s: 5.64, 6.31, 6.98 and 7.65s after the cast).
- On the fight 7 final boss, the only enemy @ 570-715s, the replay makes 257 frog attacks against the log's 268, at 0.96
  of the logged damage. Its Fire Frogs effect matches 51 of the 52 ticks, at 0.95 of the logged damage.
- Frog Squad's 20% applies to the effect as well as to the hits. Without it the effect deals 0.81-0.83 of the logged
  damage in the three fights with the talent; with it, 0.97-1.00.
- On many enemies the engine's frogs pick other enemies and times than the logged frogs, so the effect on each enemy
  holds a different share of the hits. The replay's totals stay within 0.97-1.14 of the log.
- A Pyrophibian Frenzy frog makes its first attack 1.2-1.3s after the tick that spawned it. That delay comes twice as
  often as any other among 92 frogs attacking alone (fights 7 and 19).

## Legendaries

- Item 5898 is Explosivo for Ardeos (fights 30 and 9 show Apocalypse: Explosivo damage, effect 3175, ticking every 3s).
- Item 5900 is Devouring Flame for Ardeos, as the bracers (item 5225) are. In fights 19 and 31, the logged Engulfing
  Flames damage is 1.05 and 1.16 of the replay's with Devouring Flame modeled, and 1.30 and 1.53 without it.

## Talents in fights 30 and 9

- **Cascading Inferno:** each Detonate adds a stack, and each instant Infernal Wave takes 4 (7 to 3, 8 to 4).
- **Reign of Fire:** 35 and 12 buffs, against 30.5 and 13.4 at 1.5 PPM over the Detonates cast.
- **Great Balls of Fire:** replays deal 1.02 and 1.08 of the logged Fire Ball hits, and 1.04 and 1.13 of the logged
  effect. Without the extra 10%, the effect would deal 0.69 and 0.75.

## Spontaneous Combustion (fights 19 and 31)

- A tick it empowers is a grievous crit. 10.9% and 10.8% of the Searing Blaze and Engulfing Flames ticks are grievous,
  with crit ratings of 106 and 153. The model uses 10% with no crit term.
- A replay proves only part of the procs (308 and 158 of 474 and 271 expected on Engulfing Flames): the engine's ticks
  drift from the log's by more than the 0.35s the proof allows.

## Not yet checked against a log

Backdraft, Flare Up, Agonizing Blaze, Firestarter, Crash and Burn, Crackling Inferno and Intensifying Inferno use their
tooltips and the game data. Each is marked `UNVERIFIED` in its file, as are Cascading Inferno's crit and Cinders and Reign
of Fire's Fire Ball charge.
