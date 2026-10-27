# Glyph tests: what they cover and what is open

Files: `Gear/Glyphs/` (one per glyph, plus `GlyphControl` and the shared `GlyphFights`) and `Gear/Complex/GlyphComplexStacking`, `GlyphComplexInteractions`.
680 test cases (14 failing on purpose, 4 skipped). Every glyph is tested on all six heroes and at all four ranks through theories over `GearFights.Heroes`, each
comparing a hero with the glyph against the same hero without it (ratios and differences, so a hero's own numbers cancel). Spirit Refund and Tariq's Wild Swing
avoid chance are forced off in every glyph fight (`GlyphFights.NoDice`).

## Coverage
- **The Sinister** (Core crit chance and crit power): the four heroes whose Core hit is logged under the ability (Ardeos, Elarion, Gunde, Mara) at all ranks; Basic and
  Power hits untouched; Rime's Bursting Ice pulse untouched (log rule); Butcher's Hook untouched.
- **The Trickster**: Power cast grants the crit chance at all ranks, lasts 4s, other categories (Basic, Core, Major, Spirit) do not, a second Power cast refreshes
  (no stacking), the next hits carry it.
- **The Mystic**: Power hit gains per 3% Spirit at 9%, 30%, 50% and the cap (80% reads as 50%), nothing without Spirit, Basic and Core untouched.
- **The Monarch**: Major cooldown acceleration at all ranks and hastes 0, 20%, 50%, 80% (the haste part 0, 2%, 5%, 5%), the cap at 150% haste, other abilities untouched (long cooldowns of every hero included).
- **The Philosopher**: Haste, Expertise and Crit per Spirit step at all ranks, Spirit cap, 8s duration, other categories nothing.
- **The Wayfarer**: first ability starts it, +haste at all ranks, 14s, back at 60s, a Core cast shortens it by cooldown x 4/30 (0.2 minimum), each Core cast counts, Basic, Power and Major do not.
- **The Subduer**: on at exactly 30% and below, off from 30.000001%, on Basic, Core and Power hits of every hero, on three damage over time effects (Ardeos's Searing Blaze, Mara's
  Hemorrhage, Gunde's Rend), off above 30% for them too; also reaches Butcher's Hook.
- **The Vehement**: eruption every 6/5/4/3 Basic casts for the five single-hit heroes, resets, eruption = hit x (1 + min(crit, 50%)), cannot crit, hits all enemies around,
  sqrt(5/N) beyond 5 (20 enemies), Core casts do not count.
- **Control (Gunde's Butcher's Hook)**: untouched by The Sinister, Trickster, Mystic, Monarch, Philosopher and Wayfarer; The Subduer applies to it (it is damage).
- **Complex**: Trickster + Philosopher crit add, Wayfarer + Philosopher haste add, Trickster + Sinister crit add, Subduer x Mystic multiply, Monarch's haste part counts
  the Philosopher's haste, the Vehement eruption carries the Subduer's bonus once.

## Failing on purpose (possible engine bug)
- None.

## Skipped
- **The Mystic and Ardeos's Detonate** (`GlyphMysticTests.Ardeos_Detonate_Gets_The_Bonus_Too`): the engine gives Detonate no bonus (its damage is its effects' damage per second, which no Power
  multiplier reaches; Ardeos/NOTES.md). Not known how the game behaves: to check. Ardeos is left out of the other Mystic tests and of the Subduer x Mystic test until then.
- **Tariq's Heavy Strike and The Sinister**: the engine logs Heavy Strike's hit under an effect (ID 1,001,776) and The Sinister only reaches hits logged under the ability's own ID, so Heavy
  Strike gets nothing. The rule is from Rime's Bursting Ice pulses (combat logs). Check in game whether Heavy Strike's hit gets the glyph.
- **A two-hit Basic ability and The Vehement (Gunde's Double Strike)**: the engine counts each hit, so rank 4 erupts on the 2nd cast and again on the 3rd instead of the 3rd. Check
  in game whether the cast or each hit counts.
- **The Subduer's Control cooldown reduction**: the tooltip's second part ("Your Major abilities reduce the cooldown of one of your Control abilities") is not modeled and the tooltip is cut off in the engine.

## Followed the engine (with a TODO in the test)
- **The Philosopher's refresh carry-over** (30% of the time left, so up to 10.4s): measured from combat logs, not in the game data.
- **The Vehement eruption and The Subduer**: the eruption repeats a hit that already has the Subduer's bonus, so it carries it once; whether the game counts it once is not checked.

## Not covered
- **The Wayfarer's 8 second ceiling** (needs a Core ability with a 60 second cooldown; none of the six heroes has one).
- **The Vehement's early eruption** in one log (taken as a log-parsing error), and the counter reset on the hero's death.
- **Hits that bypass the generic damage multipliers**: The Subduer's text says all damage to low health enemies, but only the paths through the hero's damage pipeline get it (not Amethyst Splinters,
  the Vehement eruption's own damage beyond the repeated hit, or other effects that call the damage function directly). Not checked per effect; each generic effect file owns its case.
- **Weapon abilities, gems and sigils with glyphs**: not combined here (e.g. The Sinister on a weapon ability, which is Weapon category, not Core).

## Notes on the fixture
- Hero probes: Rime Frost Bolt / Bursting Ice / Ice Comet / Ice Blitz / Wrath of Winter; Ardeos Infernal Wave / Fire Ball / Detonate (after a Searing Blaze) / Apocalypse / Incinerate;
  Elarion Focused Shot / Multishot / Highwind Arrow / Lunarlight Mark / Event Horizon; Gunde Double Strike / Blood Arc / Rupture / Slaughter / Bloodbound Spirit (Control: Butcher's Hook);
  Mara Skittering Blades / Widow's Bite / Queen's Fang / Maiden of Death / Matriarch Macabre; Tariq Wild Swing / Heavy Strike / Skull Crusher / Thunder Call / Raging Tempest.
