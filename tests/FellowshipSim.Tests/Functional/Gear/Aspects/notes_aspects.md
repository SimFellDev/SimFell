# Aspect tests: notes

Eight aspects (Amethyst Splinters, Diamond Strike, Emerald Judgement, Ruby Storm, Sapphire Aurastone, Brave Machinations, Martial Initiative, Visions of
Grandeur), over the six heroes (`GearHero.All`), and over each weapon ability a hero is authorized to have (`GearHero.WeaponAbilities`, a list per hero) for the three that depend on the weapon. All four ranks wherever the rank changes a
number. Cross-tests are in `../Complex/AspectComplex*.cs`.

| File | Covers |
|---|---|
| `AspectAmethystSplinters` | share by rank, ticks and their timing, haste, no crit no splinter, never crits, one per enemy, refresh keeps what was left |
| `AspectDiamondStrike` | PPM chance (haste), beam coefficient by rank, +40% per beam to 5 stacks, 20s expiry, per-enemy |
| `AspectEmeraldJudgement` | PPM chance by rank (haste), 800% Main Stat, Main Stat and Expertise scaling, crit chance, no chain, spike under the enemy hit |
| `AspectRubyStorm` | PPM chance (haste), once per cast, share of health by rank, no Main Stat or crit scaling, hits every enemy |
| `AspectSapphireAurastone` | first pulse at +6s then every 3s, ends with Spirit of Heroism, share of damage dealt by rank, even split, never crits, nothing without the Spirit ability |
| `AspectBraveMachinations` | crit chance by rank, cooldown cut by rank share on a weapon crit, once per cast, nothing without a crit, nothing without a weapon |
| `AspectMartialInitiative` | +10% Main Stat, duration by rank, ends, shows in damage, only the weapon ability gives it |
| `AspectVisionsOfGrandeur` | Spirit Points by the weapon's cooldown, cooldown cut by rank share when the Spirit ability is cast, only those, nothing without a weapon |
| `Complex/AspectComplexDiamondStrike` | Harmonious Soul's +35% a stack with the beam (and the amplification multiplying with it) |
| `Complex/AspectComplexEmeraldFirstStrike` | the spike boosted by, and refreshing, the Emerald gem's First Strike |

## Failing on purpose (possible engine bugs)
- None.

## Skipped (to check in game)

- **Brave Machinations and the Icicles of An'zhyr curse** (`A_Crit_Of_The_Icicles_Curse_Does_Not_Cut_The_Cooldown`): the engine does not count a crit of the curse's
  damage over time as a weapon crit (the test passes when run); whether the game does is to check.

## Followed the engine (a TODO in the test)

- **The class balancing damage aura** (Rime 1.04, Ardeos 1.10, Elarion 1.08, Mara 1.04, Gunde and Tariq none) multiplies the aspects' own hits
  (Emerald's spike, Diamond's beam); Ruby Storm (a share of health) and Amethyst's splinters (a share of a hit that already has it) do not. TODO check in
  game that aspect damage gets the class aura (`AspectFights.ClassDamageAura`).
- **Sapphire Aurastone stores the Spirit ability's own same-instant hits** (Rime's Wrath of Winter swallows at the instant Spirit of Heroism starts) and
  hits at a pulse's own instant may land before or after it (Mara's poison ticks do). The tests hold a pulse between the share without and with these hits.
  The rule "which hits count" is open.
- **Spirit of Heroism ends about 17ms after its 20 seconds** (a server frame): the stone and Martial Initiative tests have a 0.05s tolerance.
- **Hasted Amethyst Splinters** tick `floor(8 x (1 + haste))` times (5 at 30% haste), each a quarter of the share, so the total is slightly under
  X% x (1 + haste) (the engine's comment says it deals that).

## Unverified in the engine, tested by the tooltip

- Diamond Strike (no log has it), Ruby Storm (the hero is taken at full health; tests give a bare hero 200,000 health), Brave Machinations' cooldown part.
  Where the aspect's rule is clear in its tooltip it is tested; nothing is skipped for being unverified alone.

## Not covered

- **Earthbreaker**, a weapon ability of Gunde, Mara and Tariq: not in the engine, so no weapon-dependent aspect is tested with it. Add it to
  `GearHero.WeaponAbilities` and the names/cooldown tables in `AspectFights` when it exists.
- **Martial Initiative's 15% Damage Reduction**: the sim takes no damage, nothing to read it from.
- **Ruby Storm "targeted, offensive abilities"**: which abilities roll (the engine: any cast that completes with a damage coefficient, once per cast). Not tested which abilities do not.
- **Which damage rolls**: Diamond Strike and Emerald Judgement roll on every damage event, periodic ticks included (tested for Emerald: one roll per hit,
  ticks too). Whether the game rolls on periodic ticks is open.
- **Diamond Strike rank II Harmonious Soul** (12 stacks, 600 gem power): other gem nodes come with that power; not isolated.
- **Amethyst Splinters under extra haste** at the exact 8s boundary (a haste where the last tick lands on the 8th second) is avoided on purpose.
- **Voidbringer's Touch** needs stored damage to erupt; the weapon tests make three unit hits first.
