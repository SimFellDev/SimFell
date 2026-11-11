# Functional tests

Functional tests check the sim against the game's rules. Each test checks one rule, readable as a player scenario ("casting Frost Bolt
gives 3 Anima"). This file explains what they are for, how they run, where they go and how to write one. `Rime/RimeAbilities.cs` is a
worked example.

## Why, next to the replays and the unit tests

- **Replays** (`ExactReplayTests`, `HeroAccuracyTests`, free dice) measure how close the engine gets to real fights. They tell you that a
  number moved, not which rule broke, and they only cover what the logged players happened to do.
- **Functional tests** pin each rule down on its own: a failure names the rule ("Ice Blitz lasts 20 seconds") and the moment it went wrong.
  They also cover what logs rarely show: haste breakpoints, the edges of a window, 48 enemies, a proc forced on or off.
- **Unit tests** check one engine mechanism with a single implementation. `CooldownStateTests` covers how any cooldown reduction works,
  and the functional tests only check which effect uses it, by how much, and when.

| You want to check... | Write... |
|---|---|
| a game rule, as a player sees it | a functional test |
| how one engine mechanism computes, whatever uses it | a unit test of that mechanism |
| how close the engine gets to logged fights | nothing new: run the replays (`docs/REPLAY_ACCURACY.md`) |

```
dotnet test tests/FellowshipSim.Tests -c Release --filter "FullyQualifiedName~Functional"
```

The whole suite runs in well under a second, since every fight is a few seconds of one hero against training dummies.

## The core rules

- **Normative.** The test states the game's rule. A failure means the engine is wrong or the rule changed. Never bend a test to match the
  engine. When the game is known to differ from its tooltip (from logs or game data), the test states what the game does. When the rule is
  unverified, the test still runs, with a TODO (see Unverified rules).
- **One rule per test**, as one tooltip or one sentence of a tooltip states it. "Ice Blitz makes you deal 20% more damage for 20 seconds,
  and does not use the global cooldown" is one test. A cost and a buff, which would be stated separately, are two tests.
- **Deterministic by intent.** The test forces every random outcome it reads, so nothing depends on the seed (see Dice).
- **State first.** Assert state before and after the action. Read events only for timing, order and per-hit values (see Asserting).
- **Durations are checked on both sides of their end**, not through the engine's prediction (see Time boundaries).
- **Hit timing is not tested.** When a hit lands within a cast (spin spacing, an extra strike's delay, a proc's first attack) almost
  never matters, so no test checks it unless the user asks for one (see Time boundaries).
- **Damage is a ratio** between two fights that differ only by the thing tested (see Damage).
- **Readable as pseudo-code.** Someone who knows the game but not the engine can read the body and agree with it line by line.

## How they run

`Harness/StepDriver` runs a fight step by step: the test controls the hero, not an action list.

- `Cast<T>()` casts and returns once the hero is free again (cast, channel and global cooldown over). `TryCast` returns the refusal
  instead of throwing.
- `StartCast<T>()` starts a cast and returns at once, with the cast under way: to try another cast in the middle of it
  (`f.StartCast<FocusedShot>(); f.Wait(0.5); f.TryCast<EventHorizon>()`). `Wait` lets the cast go on. Use it extremely rarely: only in
  a test whose very point is that one action blocks another.
- `CastWhenReady` waits out exactly the ability's cooldown first, for a test that casts one ability again and again. A loop that casts
  until something happens has a bound (`for (int cast = 0; cast < 50 && ...; cast++)`), so a broken rule fails instead of hanging.
- `Wait`/`WaitUntil` let time pass.
- Between calls the fight is paused, so the test reads the state at an exact time.

Everything else is the real engine. Abilities, talents, auras and periodic effects schedule their own events, and every random outcome
goes through `SimulationContext.Outcomes`. The harness forces those outcomes by wrapping that source in decorators
(`OutcomeSourceDecorator`), so it needs no engine change and adds no game rule.

| Harness file | What it does |
|---|---|
| `StepFights` | builds bare heroes |
| `StepForcing` | forces the dice |
| `StepVerbs` | reads and sets state |
| `StepQueries` | reads events |
| `StepAsserts` | holds the tolerances |
| `TickMath` | `FullTicks` and `PartialTick`, for tick counts stated as a formula |
| `Checks` | collects several checks in one test |

Nothing outside the tests uses the harness. Its own tests are `Harness/StepDriverTests`.

## Layout

- `Harness/`: the step-by-step harness.
- `Generic/`: rules every hero shares, with no hero-specific state.
  - `Ultimate`: what every ultimate (Spirit ability) shares, as a theory over the heroes.
  - `SpiritRefund`: what a Spirit Refund gives every hero, as a theory over the heroes.
  - `CriticalStrike`: how much a critical strike deals, grievous ones included. It uses Rime's Frost Bolt as the instrument, since the rule
    does not depend on the hero.
  - `RisingSpirit`, `SpiritPoints`: the Spirit Refund's buff, and Spirit Points' generation and cap.
- `Gear/`: one file per item, over every hero that can wear it, named `<Kind><Item>.cs` in a folder per kind: `Aspects/AspectRubyStorm`,
  `Gems/GemEmerald`, `Glyphs/GlyphMonarch`, `Relics/RelicAccursedTrophy`, `Sigils/SigilKindling`, `Weapons/WeaponChronoshift`.
  `Weapons/WeaponCooldowns` has every weapon ability's cooldown, and which can be cast during the global cooldown.
- One folder per hero (`Rime/`), one file per category. Each file and its class carry the hero's name before the category
  (`Rime/RimeAbilities.cs`). All files share one namespace, so without the name the test explorer would show several `Abilities`.

| File | Covers |
|---|---|
| `ResourceEconomy` | builders, spenders, caps, passive generation (Rime: Anima and Winter Orbs) |
| `CastTimes` | how long each ability keeps the hero busy, with haste |
| `HasteBreakpoints` | how many hits a damage over time effect or a channel deals at each haste |
| `Abilities` | each ability: damage, targeting, area damage reduction, charges, buffs, and what it does with the other abilities |
| `Talents` | each talent: its own rule, and what it does with other talents and abilities |
| `Cooldowns` | base cooldowns and charges, haste, and the effects that take time off them, on their own and together |
| `Specific/` | topics only this hero has (Rime: `RimeWintersEmbrace`, which hits get the bonus at the window's edges, and the haste needed to fit casts into it) |

## Where a test goes

- **Inside a category file, one section per ability or talent** (`// ---- Icy Flow`). The section holds the rule of its own tooltip first,
  then the rules where it meets other abilities or talents. An interaction goes in the section of the ability or talent whose tooltip
  states it: Glacial Blast being instant during Wrath of Winter is in the Wrath of Winter section. A section's title names where the
  subject's other rules are, when another file has them (`// ---- Burstbolter (its extra Anima is in RimeResourceEconomy.cs)`).
- **Variants of one rule are one flat theory**, with the variant as parameters (`bool supreme, bool soulfrost, double haste`). Write every
  row out as an `[InlineData(...)]`, even for a cross product (4 variants x 4 hastes is 16 rows), so the reader sees each case. No
  `MemberData` generators, and no abstract base class with a nested class per variant.
- **An effect on its own is tested once, in its own vertical**, never repeated in every hero file. What every ultimate shares is in
  `Generic/Ultimate.cs`, and a gear effect belongs in a gear vertical over every hero.
- **A hero file tests an effect only combined with the hero's own rules**, where the combination is the point. Blessing of the Commander
  with haste on Cold Snap's hasted charges is in `RimeCooldowns.cs`.
- **No test for a chain of rules already tested one by one.** "Wrath of Winter gives Spirit of Heroism, 30% haste" (`Generic/Ultimate.cs`)
  and "haste shortens Frost Bolt's cast" (`RimeCastTimes`) need no "Frost Bolt is faster during Wrath of Winter". Test a combination only
  when it is itself a rule: when the game states it, or when the effects meet in a way the separate tests cannot show (two damage bonuses
  multiplying rather than adding, a reduction carried into the next charge).

## Anatomy

```csharp
/// <summary>A Frost Bolt gives 3 Anima when its cast completes, not when it starts.</summary>
[Fact]
public void Frost_Bolt_Gives_3_Anima_When_The_Cast_Completes()
{
    var f = StepFights.Rime();
    Assert.Equal(0, f.Amount(Rime.Anima));

    f.Cast<FrostBolt>();

    using var check = new Checks(f);
    check.Equal(3, f.Amount(Rime.Anima));
    check.Seconds(f.CompletedAt("Frost Bolt"), f.Gains("Anima").Single().Time.TotalSeconds);
}
```

- **Name:** the rule in a few words, with its numbers when they are short: `Glacial_Blast_Costs_2_Winter_Orbs`, not `GlacialBlastCost`.
  The name states a rule, not a topic. When the rule is a formula, the name gives its gist and the doc comment gives the formula.
- **Doc comment:** first, the scenario as a player reads it, in game terms, in one sentence if possible and one paragraph at most. Then,
  optionally and after a blank `///` line, at most one paragraph of details the code does not show: a tolerance, a choice of values, where
  the rest of the rule is tested. Going over these limits is a smell: the test probably does too much.
- **Comments** go at the end of the line they explain: set-up shortcuts (`// enough to cast it`), why a wait is that long
  (`// the comet falls`), what is forced and why. Never restate the code.
- **Body:** arrange, act and assert, separated by blank lines. The arrange part has two blocks:
  1. **Building the fight:** `StepFights`, talents, forcing, and set-up shortcuts such as `Set`, `Apply` or Winter Orbs.
  2. After a blank line, **the casts that set up the scenario** (Flight of the Navir before the torrent it changes). A shortcut that must
     come after a set-up cast stays with the casts.

  ```csharp
  var f = StepFights.Rime(haste: haste, talents: [.. talents]);

  f.Cast<FlightOfTheNavir>();
  if (soulfrost) f.Apply(SoulfrostTorrent.Buff); // the empowered torrent

  f.Cast<FreezingTorrent>();
  ```

  Each fight of a test with several fights follows the same shape. A scenario that checks a rule over time (a buff, then its end)
  alternates actions and checks.

## Building the fight

- `StepFights.Rime()` builds a bare hero: no gear, no talents, 0% crit, 0% Spirit. `StepFights.Of("Rime", ...)` is the same builder, for
  tests that run over several heroes. Both take `haste`, `enemies`, `talents`, `crit`, `spirit`, `mainStat`, `expertise`,
  `weaponAbilityId`, `legendaries`, `gemPower`, `glyphs`, `augmentations` (aspects and sigils) and `relics`.
  `StepFights.Ranked(TheMonarch.GlyphId, rank)` is a glyph, aspect or sigil at a rank, or nothing at rank 0.
- **The main stat is `mainStat`, never Intellect, Strength or Agility.** Every hero's main stat works the same way, and what the game calls
  it is cosmetic. Realistic values are around 100–200.
- **Set-up shortcuts** for state are fine and expected: `f.Set(Hero.SpiritPoints, 100)`, `f.Apply(GlacialAssault.Buff, stacks: 4)`,
  `f.RimeOf().WinterOrbs = 2`, `f.ResetCooldown<StarfallVolley>()`, `f.Remove(Chronoshift.Barrier)`. Say why in a comment on the line. Use the hero's own class (`f.RimeOf()`) only for the hero's own state.
  `f.Defeat(enemy)` kills an enemy, since the engine's enemies die on a schedule, never from damage. `f.SetHealth(0.26)` wounds one.
- **Real sources, not engine values.** An effect comes from what gives it in the game: a talent, a gem's power
  (`gemPower: new() { [GemColor.Emerald] = 1500 }` for Blessing of the Commander - II), a weapon. Never set an engine stat
  (`f.Hero.Stats.CooldownReductionPct = 0.1`) or add an engine hook with made-up values, or the test would pass on a rule no player can
  meet.

## Dice

Never depend on a seed. Force every random outcome the test reads:

| Outcome | How |
|---|---|
| a proc (talent, Spirit Refund, bad luck protection...) | `f.ForceProcs(key => key == Avalanche.ProcKey ? true : null)` |
| a cast's crits | `f.Cast<FrostBolt>(crit: true)` |
| the ±10% damage spread | `f.WithoutDamageSpread()` |

- **To test a chance, read it rather than roll it.** `ForceProcs` returns the procs it watches, and `procs.ChancesOf(Avalanche.ProcKey)`
  lists the chance of each roll. Force the proc to `false` when only its chance matters. Damage events carry their `CritChance`.
- These extension methods are the only entry points. Never build a decorator in a test.
- **Seed probe.** To find tests that pass only by luck, temporarily seed each `StepDriver` differently (in its constructor,
  `seed + counter * 7919`) and run the suite. Any new failure depends on the dice. Revert the probe afterwards.

## Asserting

```csharp
var f = StepFights.Rime();
Assert.Equal(0, f.Amount(Rime.Anima));

f.Cast<FrostBolt>();

Assert.Equal(3, f.Amount(Rime.Anima));
```

- **Read state through the verbs** (`Has`, `Stacks`, `Amount`, `Haste`, `Spirit`, `Expertise`, `MainStat`, `Health`, `GemPower`, `IsReady`,
  `Charges`, `AccelerationRate`, `Enemies`, `HasDebuff("Searing Blaze")`), not through `f.Hero....(…, f.Now)` or `f.Context...`. When a
  set-up line already sets the starting state (`f.RimeOf().WinterOrbs = orbs`), don't assert it again.
- **Events are for timing, order and per-hit values.** `Cast` returns only once the hero is free again, so state cannot show the middle of
  a cast: `Assert.Equal(f.CompletedAt("Frost Bolt"), f.Gains("Anima").Single().Time.TotalSeconds)`.
- **Read events through the named queries** (`Damage`, `AllDamage`, `DamageBetween`, `Crits`, `DirectHits`, `Ticks`, `Gains`, `Spends`,
  `Overflows`, `CastsStarted`, `CastsCompleted`, `AurasRemoved`, `CompletedAt`, `LastCastDuration`). Never filter `f.Events` in a test.
- **A misspelled hit name is silent.**
  - Casts and resources are looked up in the hero's own lists, and a name the hero does not have throws.
  - Auras are named by their definition (`BurstingIce.WintersEmbrace`), so a typo does not compile.
  - Hits cannot be checked: the engine keeps no list of hit names, so `f.Damage("Frost Swalows")` returns an empty list, as if nothing hit.

  A test that expects 0 hits of a name must also see that name hit somewhere. `Cascading_Blitz_Sends_A_Swallow_For_Each_Anima_During_Ice_Blitz`
  expects 3 swallows with Ice Blitz and 0 without.

**Several checks in one test.** `using var check = new Checks(f);` records each failed check instead of stopping, then fails the test with
all of them, each with its line, its expression and the fight's time (`line 140, at 20.100s: f.Has(IceBlitz.Buff) is true`). It has
`True`, `False`, `Equal`, `Ratio` and `Seconds`. If the test throws after a check failed, it reports the failed checks rather than the
exception.

- Use `Checks` when the test checks several clauses of a rule. A single clause stays a plain `Assert`, and so does a before/after pair
  around one end time (see Time boundaries), which is one clause.
- `Checks` is test tooling, so it stays out of the scenario. Declare it as the first line of the first block that checks, after a blank
  line, never among the set-up or the actions. Lines that measure (`var comets = f.Damage<IceComet>();`, `double end = ...`) come before
  it, with the actions.
- Add a `what:` label only when the expression does not say what the value is (in a loop).

## Time boundaries

Check a duration by its effect on either side of its end:

```csharp
f.Cast(ability);
double end = f.CompletedAt(ability) + cooldown;

f.WaitUntil(end - 0.1);
Assert.False(f.IsReady(ability));
f.WaitUntil(end + 0.1);
Assert.True(f.IsReady(ability));
```

- This applies to buffs, debuffs, cooldowns and charges alike.
- **Measure from what starts the effect.** Most effects start when the cast completes (`f.CompletedAt(...)`), not at 0.
- `WaitUntil(t)` takes a time on the fight clock. Use `Wait(seconds)` only to let things land (`f.Wait(4.0); // the comet falls`).
- Avoid hastes that land exactly on a breakpoint, unless the breakpoint is the point.
- A channel's cast completes when the channel starts, so `LastCastDuration` is 0 for it. Its length is the time the cast returns
  (`f.Seconds()` after `f.Cast<FreezingTorrent>()` at 0s).

### Hit timing

Precise timing is irrelevant almost all the time. Test how long a buff, debuff, cooldown or window lasts, as above: that decides what
lands in it. Do not test when a hit lands within a cast (the spacing of spins within a cast, an extra strike's delay, a proc's or
a summon's first attack, a travel time), unless the user asks for that test. A small time offset is out of the sim's current scope: it
does not model flight time for most abilities. Waiting long enough for the hits to land (`f.Wait(4.0); // the comet falls`) is fine.

## Damage

```csharp
var plainScenario = StepFights.Rime().WithoutDamageSpread();

plainScenario.Cast<FrostBolt>();
double plainBoltDamage = plainScenario.Damage<FrostBolt>().Single().Value;

var blitzScenario = StepFights.Rime().WithoutDamageSpread();

blitzScenario.Cast<IceBlitz>();
blitzScenario.Cast<FrostBolt>();
double blitzBoltDamage = blitzScenario.Damage<FrostBolt>().Single().Value;

AssertRatio(1.2, blitzBoltDamage / plainBoltDamage);
```

- **Name each fight after what sets it apart**, with a `Scenario` suffix (`plainScenario`, `blitzScenario`). `f` is for a test with a
  single fight. Name each damage as soon as it is read (`plainBoltDamage`), so the assertion reads as the rule. Long names are fine.
- **When the fights differ by one parameter**, a local function that builds and runs one fight is clearer than two copies:
  `AssertRatio(1.3, FirstPulse(Burstbolter.TalentId) / FirstPulse())`.
- **Both fights** carry `.WithoutDamageSpread()`. It departs from the game, so it is off by default and a test opts in, visibly.
- **Crits are decided.** The bare hero has 0% crit, and a hero with crit (gear, a weapon) casts with `crit: false` or `crit: true`.
- **Game values are literals** in the test (`1.2`, `0.3`), not engine constants.
- **Credit an effect to what gives it in the game.** An ultimate's 30% haste comes from Spirit of Heroism, the buff every ultimate gives:
  "Wrath of Winter gives Spirit of Heroism, 30% haste", never "Wrath of Winter's 30% haste".

## Tolerances

- `AssertRatio(expected, actual)` uses 0.001, and `AssertSeconds(expected, actual)` uses 2ms, since the engine rounds times to the
  millisecond. Expected comes first, as in xUnit.
- Assert counts, stacks and resources exactly, with `Assert.Equal`.
- **A rule stated as a formula is checked against the formula**, written out in the test from the row's inputs
  (`int hits = FullTicks(3.0, tick) + PartialTick(3.0, tick, 0.1)`), not against numbers worked out by hand.
- **Tick counts**: floor(duration / period x (1 + haste)), plus 1 when a last partial tick comes. Its threshold is the time left after
  the last full tick:
  - a damage over time (a Dot or Hot effect) gives a partial tick when at least 0.1 seconds (3 server frames) are left:
    `PartialTick(duration, tick, 0.1)`;
  - another periodic effect, such as a channel whose hits are counted, gives one when at least a server frame (1/30 second) is left:
    `PartialTick(duration, tick, PeriodicEffect.ServerFrameSeconds)`. This one is not fully confirmed in game; say so in the doc comment.
- A wider tolerance needs a reason in the doc comment.

## Naming abilities

Use one vocabulary per test:

| The test is about... | Name the ability by... | Example |
|---|---|---|
| one specific ability | its type | `f.Cast<FrostBolt>()`, `f.Damage<FrostBolt>()` |
| one rule over a list of abilities | its game name | `[InlineData("Bursting Ice", 10.0)]`, then `f.Cast(ability)` |
| one rule over several heroes | (hero, ability) game names | `[InlineData("Tariq", "Skull Crusher")]`, then `StepFights.Of(hero)` and `f.Cast(ability)` |

A game name with a typo fails loudly, listing the hero's abilities. Hits that no ability is named after (Frost Swallows, Coalescing Frost)
are read by their game name: `f.Damage("Frost Swallows")`.

## Helpers

- Use the harness first. A pattern needed in several files belongs in `Harness/`, not copied from file to file.
- A local helper earns its place when it is used several times in its file and its name says what it does (`ColdSnapWhenReady(f)`). A
  helper that only wraps a harness call is noise.
- A helper sets up or measures. It never hides the assert. The one exception to "never hides the act" is the fight-building local
  function of Damage, where the test is the comparison.

## Unverified rules

A test whose rule is not confirmed still runs. Its doc comment says where the engine's behaviour comes from (a log, "per the user", or
nothing) and has a `TODO` saying what to confirm: "TODO: confirm in the game data whether the threshold counts the blast's own target"
(Glacial Assault's explosion).

Skip a test only when the engine disagrees with a confirmed rule, with a reason naming what the engine does. A skipped test is still a
written test, never a placeholder with `Assert.Fail`.

## Open questions

- **Hero-specific state** (`f.RimeOf().WinterOrbs = 2`): keep the hero's own property, or use a generic
  `f.Set(Rime.WinterOrbsResource, 2)`?
- **Hits that are not named after their ability:** should they get typed accessors, or stay as strings?
