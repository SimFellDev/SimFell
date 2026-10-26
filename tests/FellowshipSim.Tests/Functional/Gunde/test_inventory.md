# Gunde test inventory

No Gunde tests exist in the Python sim (`fellowship_elarion_sim`) or the C++ tests (`fellowsimc`), so nothing was ported. The tests were written from the engine's code (`Heroes/Gunde/`: the tooltips quoted in each ability and talent, `NOTES.md`), one behaviour per test, rules from the tooltips and values from the logs as the notes give them. They are to be reviewed against the game. Hits are compared with an auto attack swing (30% of Strength) as the unit, as Mara's are with Skittering Blades.

## Files

| File | Covers |
|---|---|
| `ResourceEconomy` | no resource of his own, Blood Feathers (Rend ticks, enemy deaths, the 150 cap), Spirit Refund (5 feathers, a Spirit Point, one roll per Rend cast, none without Rend) |
| `CastTimes` | the 1.5s hasted global cooldown of the Rend abilities, off-global abilities, a rotation |
| `AbilitiesSimple` | damage coefficients, hit counts, single target and area falloff, spin timing, the Rend share of each ability, Rend's ticks, Exsanguinate, Slaughter and Open Wounds, Serrated Edge, Reign in Blood, Bloodbound Spirit, Owed in Blood |
| `AbilitiesComplex` | Rend from several abilities, Reign in Blood with Serrated Edge, Owed in Blood with Rend, Exsanguinate and Slaughter, Rising Spirit, when the refund comes |
| `TalentsSimple` | all 18 talents, one each (Death's Arc, Raven's Precision, Grim Harvest, Sundered Flesh, Crimson Strikes, Deep Rend, Darkening Hearts, Murder of Crows, Massacre, Slayer's Grin, Frenzied Reign, Bloodbath, Bloodcraze, Oathshatter, Carnage, Superior Serration, Ancestral Instinct, Harvester's Toll) |
| `TalentsComplex` | every Rend bonus on one Heart Splitter (1.85), Deep Rend with Owed in Blood and Slaughter, Crimson Strikes with Serrated Edge, Exsanguinate with damage multipliers, Death's Arc's crit used up |
| `CooldownDurationSimple` | base cooldowns, haste on the four hasted abilities and the auto attack, Warbound's charges |
| `HasteBreakpoints` | Rend, Slaughter, spins and pulses are not hasted |

There is no `CooldownDurationComplex` (the talents that cut cooldowns are in `TalentsSimple`: Carnage, Slayer's Grin, Bloodbath, Death's Arc, Grim Harvest).

## Topics left for `Specific/` (not tested; for review)

- **Rend's accounting:** an application ends after its own ticks; Deep Rend's 82 server frames; the first 15 stacks of Owed in Blood count one each and the rest 0.85 (the engine's fit of the logs); the Rend stacks shown on the enemy.
- **Sundered Flesh** with Slaughter, and its effect on the Exsanguinate that follows.
- **Crimson Strikes and Grim Carve:** a spin cast before a Blood Arc but landing after it uses the buff without the bonus; Bleeding Hearts' extra strike has no share of it.
- **Bleeding Hearts / Bloody Mess** (a legendary or a set): the extra strike 0.23-0.30s after the cast with its own Exsanguinate, at 0.84 of the cast's strike; extra spins of Grim Carve; Heart Splitter with 2 charges.
- **Bloodbound Spirit and Drakheim's Absolution:** the +20% Main Stat on the first pulse, and every pulse keeping the damage of the first.
- **Owed in Blood's replay behaviour:** a cast using stacks removed in the same instant; a pickup at 150 stacks is a refresh.
- **Heart Splitter's crit rules:** crit chance past 100% (Death's Arc, Grim Harvest, Darkening Hearts and Oathshatter together) is a grievous crit.
- **Fated Strike** (a legendary: cleave hits logged as Fated Strike at 2.874 / 7.39 x sqrt(3/N)).

## Not tested and why

- Reckless Abandon's 40% damage reduction and Jawbreaker's interrupt: no damage dealt, no enemy casts in the engine.
- Bloodbound Spirit's global cooldown and its Spirit Points cost beyond needing them: whether it takes the global cooldown is not known (no cast time since patch 0.4.5.0).
- Double Strike at range (its hits land 0.24-0.30s and 0.44-0.50s after the cast): the tests are in melee.
- Passive Spirit regeneration (a Spirit Point every 3s shows in the fights): no rule to check against.
- Death's Arc, Grim Harvest and Ancestral Instinct beyond their chances and effects (cooldown reset when the ability is already ready).
