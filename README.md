# SimFell

A combat simulator for *Fellowship* (C#, .NET 10). It models heroes' abilities, talents and gear, and checks itself
against real combat logs from [FellowshipLogs](https://www.fellowshiplogs.com).

Supported heroes: **Rime**.

SimFell runs in three ways. They share the same engine:

- **Web app**: a Blazor page served by FellowshipSim.Service. The engine runs in its own process.
- **Desktop app**: a .NET MAUI app for Windows and macOS. It shows the same pages and runs the engine in-process.
- **`simfell`**: a command-line tool that sims a `.simc` profile in-process.

## You need

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) with npm: the build bundles the code editor (CodeMirror)
- The [Aspire CLI](https://aspire.dev), to run the web app
- The .NET MAUI workload (`dotnet workload install maui`), to build the desktop app
- To download fights, a FellowshipLogs account: **Sign in with FellowshipLogs** (top bar, or the Import cards) signs you in
  with SimFell's public API client (OAuth with PKCE, no secret), and your requests spend your own FellowshipLogs points
  first. The browser app comes back to `/signin`; the desktop and local apps open your browser and catch the way back on
  `http://127.0.0.1:47213/signin`. A developer can instead put an API client of their own in `settings.json` at the
  repository's root, which git ignores: `{ "FELLOWSHIPLOGS_CLIENT_ID": "...", "FELLOWSHIPLOGS_CLIENT_SECRET": "..." }`.
  Every app's build copies it in

## Build

```
dotnet build -c Release
```

On Windows the solution builds the desktop app for Windows; on macOS it builds it for Mac Catalyst.

## Run the web app

```
aspire run
```

Aspire starts the engine and FellowshipSim.Service, and prints the dashboard address. Open the app at
http://simfell.dev.localhost:5000. It opens on the **Sim** page. The top bar has the other sections, **Log Replay**
(import a fight, and the reviews) and **APL Editor**; the **Logs API** badge opens the settings. On macOS, port 5000 is
taken by AirPlay Receiver: turn it off in System Settings > General > AirDrop & Handoff.

Each browser is a visitor, known by a cookie the app sets on the first visit. Every tab of that browser is the same
visitor. A visitor sees only their own sims, replays, reviews and action lists, and cancels only their own jobs. All
visitors share one job queue, and each queued job shows how many jobs are ahead of it. A visitor's files are in
`data/visitors/<visitor id>/`. The cookie keys are in `data/keys/`, so visitors keep their files when the app restarts.

The web app downloads fights with the host's FellowshipLogs API client. Set its credentials before you start the app:

```
# PowerShell
$env:FELLOWSHIPLOGS_CLIENT_ID = "..."; $env:FELLOWSHIPLOGS_CLIENT_SECRET = "..."
# bash
export FELLOWSHIPLOGS_CLIENT_ID=... FELLOWSHIPLOGS_CLIENT_SECRET=...
```

In development, the web app, the desktop app and `tools/fetch-fights.cs` also read them from the `simfell` user secrets:

```
dotnet user-secrets set FELLOWSHIPLOGS_CLIENT_ID ... --id simfell
dotnet user-secrets set FELLOWSHIPLOGS_CLIENT_SECRET ... --id simfell
```

Without either, the app reads the repository's `settings.json` (see above).

To download and replay a fight:

1. On **Import**, paste a FellowshipLogs report link and load it.
2. Click a player. The fight is downloaded and replayed as a job. When the job is done, its review page opens. The
   **Reviews** page lists your reviews.

## Run the desktop app

```
dotnet run --project src/FellowshipSim.Desktop -c Release -f net10.0-windows10.0.19041.0
```

On macOS, use the target framework `net10.0-maccatalyst`. Its data folder is `FellowshipSim` in your local application
data (`%LOCALAPPDATA%\FellowshipSim` on Windows). The data folder is yours alone: every review in it is listed, and
Its FellowshipLogs client ID and secret come from the repository's `settings.json` (see above).

The desktop app has the same pages as the web app, each as a tab. Starting a sim opens its tab, which shows its progress
and then its result. A replay started from **Import** opens its review page in a tab when it finishes. Picking a result
from the **Results** sidebar also opens its tab. You can close these tabs; the page tabs stay. A tab keeps what you
entered while another tab shows.

When the result tabs don't fit, ‹ and › scroll them, as does the mouse wheel over them. The **N open** list beside them
names every open result tab, selects or closes one, and closes them all.

## Run the browser app

The browser app is the same app as a static site: every sim and replay runs on the visitor's own computer, and nothing
runs on a server. Sims spread over web workers, one per core but the page's (up to 16); each worker is a .NET runtime of
its own, and a sim gives the same result as on the desktop app. Its files stay in the browser (IndexedDB): builds and
action lists, settings, the newest 20 sims and the newest 10 reviews. A replay downloads its fight on a worker, which keeps
the newest 5 fights in the browser's storage apart from the page, so replaying one again spends no FellowshipLogs points. Builds and action lists
also download as `.simc` files. Visitors sign in with their own FellowshipLogs account to import and replay fights; it
takes no API client from them. Locally its build takes the repository's `settings.json` as its `wwwroot/appsettings.json`.

```
dotnet run --project src/FellowshipSim.Web
```

The **Deploy web app** workflow (Actions, run by hand) publishes it ahead-of-time compiled to
https://simfelldev.github.io, from the public SimFellDev/simfelldev.github.io. It needs the secret `SIMFELL_PAGES`, a
fine-grained token with "Contents: Read and write" on that repository alone.

## Sim a profile

A profile is a `.simc` file: the hero, stats, talents, traits, blessings, gear and its `actions` lines, which are the
action list. Keep profiles in `data/profiles/<Hero>/`.

A sim's profile also has the sim settings: `iterations`, `threads`, `seed`, `duration` and `targets` (training
dummies), `length_variance` and `hero_share_variance` (fractions), `health_follows_time`, `boss`, `starting_spirit`,
`reaction_delays` and `proc_reaction` (seconds). A dungeon fight has a dungeon's lines in place of `duration` and `targets`. The lines are in any
order. The last line of a setting is the setting, and a `dungeon=` line starts a dungeon that replaces the one before.
Every option of the **Sim** page is a line of this text, and a sim's job is the text alone.

The **Sim** page loads a profile file or pasted text, sets iterations and the fight, and starts the sim.
The sim's page (a tab in the desktop app) shows its progress, then its result: mean, minimum and maximum DPS, the
standard deviation, and DPS for each ability. **Save as HTML** saves the result as an HTML file. **Advanced**, at the
end, has the sim's profile. **Load** puts its build, action list and settings in the Sim page, as a compare run's
**Load** does.

### Fights

The **Modes** tab has two kinds of fight:

- **Training dummies** (the default): a number of training dummies for the whole duration. **The dummies are bosses**
  sets whether boss-only effects (Ruby's Blessing of the Conqueror) count.
- **Dungeon**: the pulls of a dungeon in turn, each until its enemies are dead, up to the dungeon's time limit. An enemy
  with hit points dies from damage: the hero deals the dungeon's hero share of it (37.5% by default) and the party the
  rest. An enemy without hit points has health falling over its pull's duration. Between pulls is downtime, with no
  enemies and no actions. The fight's DPS is over its whole length, downtime included, and the result adds the DPS to
  bosses. The Dungeon card starts with the built-in `Default` dungeon: Cithrel's Fall's creatures in four trash pulls and
  two bosses, Cithrel the final boss.

**Proc reaction (ms)** in the **Fight settings** card is the time a proc's gain takes to reach the action list: 300 ms by
default, as SimulationCraft's player `reaction_mean`. Until it passes, `buff.<proc>` reads the proc as it was before the gain:
down for a new proc, the stacks before a new stack, and the time left before a refresh. 0 shows each gain at once. The
command line's `--proc-reaction <ms>` is the same setting.

The **Dungeon** card has a comment, the hero share, the time limit, the difficulty and the pulls. **Vary
the hero share** gives each fight a hero share in a range: ± 20% by default, so 37.5% is 30 – 45%. Each pull has a
comment and a name in its head, and opens to its name, its start (after downtime, or chained to another pull's end), its
pull duration, the final boss, another pull it takes in with that pull's trigger, and its enemy groups. A group has a
comment, a count (trash), a name, Spirit Points, hit points, priority, and an interrupt ability with its cooldown and cast
time. A new pull's name is the lowest whole number no pull has. Renaming a pull renames it in every `chain_with` and
`pull_with`. **Save** writes `data/dungeons/<name>.simc` in the desktop app and `data/visitors/<visitor id>/dungeons/` in
the web app.

**Import from a log** on the **Import** tab imports the player picked: their build with **Build** on, the
dungeon of the run (with their hero share) with **Dungeon** on, and both at once with both on. The dungeon import
downloads the fight as a replay does (`Engine/Replay/DungeonReader`):

- The enemies are the ones the party damaged and killed. Each has its hit points from its log health at the run's
  difficulty, and its Spirit Points from game data.
- The pulls are FellowshipLogs' pulls of the fight, numbered as FellowshipLogs lists them. Each enemy is in the pull it
  died in, and a pull in which no enemy died is left out. A pull's downtime is the time since the pull before it
  ended.
- An enemy arrives at its first hit with the party in its pull, and enemies arriving within 5 seconds of each other are a
  wave. A pull's later waves (`pull.<pull>_<n>`) join the wave before them through `pull_with` at their log offset, so
  the enemies in combat at once are the log's.
- An enemy's interrupt ability is the ability of its name the party interrupted most, with its cooldown and cast time from
  the log.
- The hero share is the player's damage to those enemies over their health.

**Dungeon text** has the dungeon as text:

```
# Cithrel's Fall, two trash pulls before the first boss.
dungeon=Default
difficulty=10
hero_share=37.5
time_limit=1800
# Imps and their captain.
pull.1=/count=5,name=Imp,spirit_points=3,hit_points=7857
# Big Cast is interruptible.
pull.1+=/count=1,name="Ogre Captain",spirit_points=10,priority=1,hit_points=11546,interrupt_ability="Big Cast",cooldown=20,cast_time=2.5
pull.2=/count=4,name=Ghoul
pull.2+=/pull_duration=45,downtime=20
pull.3=/count=3,name=Bat,spirit_points=2,hit_points=3374
pull.3+=/chain_with=2
pull.last_boss=/final=1,downtime=30,pull_with=wisps,offset_pct=50
pull.last_boss+=/boss.1,name="Ancient Koros",spirit_points=45,hit_points=64029
pull.wisps=/count=2,name=Wisp,hit_points=15145
```

A `#` line is a comment. A comment above a `pull.X+=/` enemy group line belongs to that group, a comment above any other
pull line or after the last pull belongs to the pull, and any other comment belongs to the dungeon. A pull whose first
group has a comment starts with its options line, or with an empty `pull.X=/` line.

`difficulty` is an Eternal level, 0 for Base. Each difficulty multiplies creature health and damage by the game's scale
for it: 1.31 at Base, 58.26 at Eternal 10 and 351.49 at Eternal 32 for health.

`pull.X=/` starts pull X and `pull.X+=/` adds a line to it. A pull name has letters, digits and `_`. A line is an enemy
group (`count=`), a boss (`boss.N`; several make a council) or the pull's options. Group values: `count`, `name` (in
quotes when it has a comma), `spirit_points` (each enemy's), `priority=1`, `hit_points` (the creature's game data
health, before the difficulty scale), `interrupt_ability`, `cooldown` and `cast_time` (2.5 s unless given; the first cast
is one cooldown after the enemy arrives). Pull options:

- `downtime`: seconds before the pull starts, after the pull before it ends.
- `chain_with=X`: the pull starts as pull X ends, with no downtime.
- `pull_with=X`: pull X's enemies join this pull, at its start or at one trigger: `offset_ms` after the start,
  `offset_pct` (the pull's average health %), `after_deaths` (the pull's own enemies dead) or `after_deaths_total` (all
  enemies of the combined pull dead). A death count is a number or a percent (`50%`).
- `pull_duration`: seconds over which the health of enemies without hit points falls.
- `final=1`: the pull's boss is the final boss.

Pulls run in the order of their `pull.X=/` lines. A pull taken in by another runs with it, and a chained pull runs as the
pull it names ends. The fight's length variance varies each `pull_duration`.

Action lists read the fight with these values:

| Value | What it reads |
|---|---|
| `dungeon_event.adds.exists` | 1 while a dungeon enemy that is not a boss is in combat |
| `dungeon_event.adds.count` | the enemies in combat that are not bosses |
| `dungeon_event.adds.in` | seconds until the next pull with enemies that are not bosses, an estimate; 10000 when none follows |
| `dungeon_event.boss.exists` | 1 while a boss is in combat, or with training dummies that are bosses |
| `dungeon_event.boss.count` | the bosses in combat (3 in a council of three) |
| `dungeon_event.boss.final` | 1 while the final boss is in combat |
| `dungeon_event.boss.in` | seconds until the next pull with a boss, an estimate; 10000 when none follows |
| `dungeon_event.fight.remains` | seconds until the fight ends, an estimate in a dungeon |
| `target.is_boss` | 1 when the target is a boss |
| `target.is_priority` | 1 when the target is a priority target |
| `target.time_to_die` | seconds until the target dies, from its health loss so far, its pull duration, or the training dummy fight's end |
| `target.casting` | 1 when the target is casting an interruptible ability |
| `target.cast.remains` | seconds until the target's cast ends; 0 when it is not casting |

Rime's Brain Freeze and Gunde's Jawbreaker interrupt a casting target. An uninterrupted cast has no effect.

A boss that is not the final boss is `dungeon_event.boss.exists&!dungeon_event.boss.final`. A list with
`raid_event.<x>` reads it as `dungeon_event.<x>`, and `fight_remains` as `dungeon_event.fight.remains`; the editor
shows and saves the new names.

Each finished sim is saved in `data/results/` (the desktop app) or `data/visitors/<visitor id>/results/` (the web app),
so it stays after a restart.

The **Results** button at the top right counts your running jobs. It opens a sidebar with your sims and replays still
in the queue, with their progress and buttons to cancel or remove them, then your recent sims and replays, newest
first. Select one to open its page. Two jobs run at once and the rest wait in the queue.

### Sim from the command line

```
dotnet run --project src/FellowshipSim.Cli -c Release -- --profile data/profiles/Rime/PROFILE.simc --iterations 10000 --threads 8
```

Replace `PROFILE` with the name of your profile file.

`simfell` writes the result to stdout and its progress to stderr. Ctrl+C cancels the sim. You can run several at once.
Each option is a line added at the end of the profile, so it replaces the profile's own line for that setting. The
options are the following:

- `--profile <file>`: the profile to sim (required)
- `--iterations <n>`: fights (default 100 for a profile with no sim settings)
- `--dungeon <name|file>`: fights of a dungeon, built in (`Default`) or a dungeon `.simc` file; its pulls set the enemies and its time limit the longest fight. Without it, the fights are training dummies
- `--duration <seconds>`: length of each fight of training dummies (default 300)
- `--targets <n>`: training dummies in each fight (default 1)
- `--seed <n>`: seed of the first fight; fight i has seed + i (default 1)
- `--threads <n>`: most threads the fights use (default 80% of the logical processors)
- `--format <text|json>`: a text summary, or the `SimResult` record as JSON (default `text`)

## Edit action lists

The **Action lists** page edits an action list without writing its text. Each action has an
ability, a condition, a target and a comment; drag the handle or use the arrows to change the order. An action's **Type**
is an ability to cast, **Run action list** or **Call action list** with the list to run, or **Set a variable** with the
variable's name, its op (set, add to, subtract from or reset), its value and its default at the start of each fight;
conditions read the variable as `variable.<name>`. An ability's **Timing** switches are **Use off the global cooldown**
(`use_off_gcd=1`) and **Use while casting** (`use_while_casting=1`). The condition, a target's **Target by** and a
variable's value each have **Builder** and **Text**. Each name in a
condition has a colour for its kind (buff, debuff, cooldown, resource), and a wavy underline marks a name the hero
does not have. Conditions in the editor chain with `and`, `or` and `not`; the text has `&`, `|` and `!`. A `&`, `|`
or `!` typed in the editor becomes its word when you leave the field. Parentheses stay as written. **Builder** shows a
condition as rows: each row has its `and` or `or`, its `not`, and a value compared in turn to further values. A
**Group** is a box of rows in parentheses; its chevron closes it to one line of its text, and **Close all groups** closes
every group. Each value is terms joined by `+`, `-`, `*`, `%` or `%%`. Each term's picker lists the names by kind (buff,
debuff, dot, cooldown, action, resource, fight value, value of the action itself, talent, blessing, legendary, weapon
trait, weapon, enemies with a debuff, variable, previous action; a debuff's and a dot's properties include the shortest and
longest time remaining on any enemy, `min:` and `max:`), then **Number** and **Group** for a condition in
parentheses, then each **Function**, then `not` or `-` before the term; typing filters the list. A function takes the term
as its first argument and shows each argument in its own box. The empty select after a value adds a term, a comparison, or inside
parentheses an `and` or `or` row; an emptied operator or comparison removes its term or value. **Text** shows the
condition as text. Typing a name, a `.` or Ctrl+Space lists the names that fit; Enter or Tab writes the one picked.
**Action list text** has the text of the list, coloured and completed as a condition is, with each name the hero does not have and the first line
that does not parse marked. An edit there goes into the editor once it parses. **Save** writes `data/apls/<hero>/<name>.simc` in the desktop app. In the web
app, **Save** writes one of your own lists in `data/visitors/<visitor id>/apls/`, and **Saved** also lists the shared
lists in `data/apls/`, marked "shared". A shared list loads into the editor, and saving it makes your own copy.

A target picks the enemy for the cast, written `target_if=` in the text:

- `target_if=min:debuff.<name>.remains`: the enemy with the lowest value (`max:` for the highest)
- `target_if=debuff.<name>.down`: the first enemy where it is true; the action is skipped when there is none

`target.*` and `debuff.*` in the action's `if` read the enemy picked.

A decision runs whenever the hero is free to cast. Two options of an ability action add decisions while a hold runs, as
in SimulationCraft:

- `use_off_gcd=1`: the action is a candidate while the global cooldown runs (Ardeos's `incinerate,use_off_gcd=1`)
- `use_while_casting=1`: the action is a candidate while a cast or a channel runs, for an ability off the global cooldown
  (Rime's `ice_blitz,use_while_casting=1`)

Without them, the action waits for the global cooldown, the cast or the channel to end.

Conditions also have these values and functions:

- `prev.<ability>`: 1 when the last cast the hero started is that ability (`prev.frost_bolt`)
- `prev_gcd.<ability>`: 1 when the last cast the hero started on the global cooldown is that ability. A cast off the global
  cooldown (Ice Blitz) leaves it as it is (`prev_gcd.frost_bolt`)
- `debuff.<name>.oldest.remains` and `debuff.<name>.newest.remains` (and `.refreshable`): the application applied first or
  last, for a debuff whose every application runs on its own (`debuff.engulfing_flames.oldest.remains`)
- `min:debuff.<name>.remains` and `max:debuff.<name>.remains`: the shortest or longest remaining time of any application on
  any enemy in combat. Enemies without the debuff are left out. With no application anywhere, `min:` is a very large
  number and `max:` is 0 (`incinerate,if=min:dot.engulfing_flames.remains<gcd`)
- `abs(x)`, `ceil(x)`, `floor(x)`, `round(x)`, `min(a, b, ...)` and `max(a, b, ...)`
- decimal numbers (`1.5`, `.25`)

Each decision starts at the default list (`actions+=/`). **Add action list** adds a named list (`actions.aoe+=/`), and an
action's **Type** runs it:

- `run_action_list,name=aoe`: `aoe` runs, and the actions after this one do not
- `call_action_list,name=aoe`: `aoe` runs, then the next action when nothing in `aoe` is cast

Both take an `if` like any other action. A list the decision is already in is skipped. A list named by
`run_action_list` has a warning until it has an action that is always ready: an ability with no cooldown and no cast
requirement (Frost Bolt for Rime), with no condition or target.

## Debug an action list

The **APL Debugger** fights one fight of a build with an action list and keeps every decision. Two buttons open it:

- **Replay in debugger** on a sim result fights the sim's typical fight again: the run's build and action list, the
  sim's settings, and the typical fight's seed. The message says whether the fight's casts are the typical fight's. A
  sim saved before sims kept their settings has the button off; run it again to replay it.
- **Debug** in the APL Editor opens the list as it is in the editor, unsaved edits included. **Player profile** picks
  the build: the Sim page's build of the list's hero, a saved build or a new build. The **Fight** and **Fight settings**
  cards are the Sim page's Modes tab's, with settings of their own. **Fight settings** also has the **Seed**: any 32-bit
  integer, 1 at first, and **Randomise** picks one from the current time. **Run fight** fights the first fight of a sim
  with that seed.

Each decision shows its time, the ability it cast and its target, and the action lines it read in order, indented for
each list entered. Each line has its outcome: **Cast**, **Condition false**, **Not ready** (a cooldown, a resource or a
cast already running), **No target**, **Cast refused**, a variable's value, or a list **Entered**. A line opens to show
its condition as a tree of expressions with each one's value, and its `target_if` value at each enemy. A side of
`&` or `|` the decision never read shows as not read. A **Cast refused** line opens to the engine's reason, such as the
global cooldown, or to each proc gained inside the proc reaction time. Beside the lines are the hero's resources, buffs, the global cooldown left, cooldowns and variables, and
each enemy's health and debuffs, before the cast. **Previous** and **Next** (or the arrow keys) step one decision;
**Previous cast** and **Next cast** step to the next decision that cast, and **Go to** (seconds or `m:ss`, then Enter) shows the last decision at or before that time.
## The two checks

- **Exact replay** (the review pages): every crit, proc, target, landing time, damage amount and periodic tick time is
  the log's, and each logged value is checked against the range the engine computes for it. A review page lists each
  value outside its range under Disagreements, with the engine's state at that moment. Good for finding which mechanic
  is wrong.
- **Free dice**: only the casts follow the log; the engine rolls its own crits and procs, many times over. The average
  should match the log, and the log should look like a typical run. Good for overall accuracy.

The review list shows both. Free dice has its own page per fight (click its number in the list).

## Run free dice

Free dice runs from the tests, over fights you have already downloaded with the web app or the desktop app:

```
# every downloaded fight, 1000 runs each (about 1.5 minutes)
FREEDICE_REVIEW=all FREEDICE_ITERATIONS=1000 dotnet test tests/FellowshipSim.Tests -c Release --filter FreeDice

# one fight (the review ID from data/reviews, e.g. yMAZVJL9hTpFjqbC_fight7_p1)
FREEDICE_REVIEW=yMAZVJL9hTpFjqbC_fight7_p1 FREEDICE_ITERATIONS=1000 dotnet test tests/FellowshipSim.Tests -c Release --filter FreeDice
```

In PowerShell set the variables first: `$env:FREEDICE_REVIEW = "all"; $env:FREEDICE_ITERATIONS = "1000"`.
`FREEDICE_SEED` sets the first seed (default 1, so runs repeat exactly; `random` for a new one).
Refresh the page afterwards to see the new numbers.

## Tests

```
dotnet test -c Release
```

Runs the engine tests and replays every downloaded fight, rewriting its review page.

## Where things are

| Folder | What's there |
|---|---|
| `src/FellowshipSim.Core` | The engine: simulation, heroes, talents, gear, `.simc` profiles (`Import/SimcProfile.cs`). Knows nothing about logs |
| `src/FellowshipSim.Logs` | FellowshipLogs client, and the data folder's fights and reviews. Knows nothing about the engine |
| `src/FellowshipSim.Engine` | The engine library: turning a logged fight into a replay script, exact replay and free dice, their pages (`Replay/`), `ISimEngine` and its in-process `LocalSimEngine`, and the sim job queue (`Jobs/`) |
| `src/FellowshipSim.EngineServer` | The engine service: `LocalSimEngine` over HTTP. Only FellowshipSim.Service can call it (shared key) |
| `src/FellowshipSim.UI` | Every page, component and style (Razor class library): the pages (`Pages/`), the web app's frame (`Layout/`) and the desktop app's tabs (`Shell/`), the import and review parts (`Replay/`), the Results sidebar and job pages (`Jobs/`), the action-list editor (`Apl/`) and the app services (`Services/`). Each component has its styles in `X.razor.css` beside `X.razor`; `wwwroot/css/app.css` has the colours, fonts and element defaults, and `wwwroot/css/design-system.css` the classes several components share. `AddSimFellUI` registers the services |
| `src/FellowshipSim.Service` | The web app: a Blazor Web App (Interactive Server) hosting FellowshipSim.UI. `RemoteSimEngine` calls the engine service |
| `src/FellowshipSim.AppHost` | Aspire: runs the engine service and FellowshipSim.Service together |
| `src/FellowshipSim.Desktop` | The desktop app: FellowshipSim.UI in a .NET MAUI BlazorWebView, with the engine in-process |
| `src/FellowshipSim.Web` | The browser app: FellowshipSim.UI in Blazor WebAssembly, sims and replays on web workers (`wwwroot/sim/`), its files in the browser's IndexedDB (`BrowserDataFolder`) |
| `src/FellowshipSim.Cli` | `simfell`, the command-line sim |
| `tests/FellowshipSim.Tests` | Tests, including the free-dice runner |
| `docs/` | [Adding a hero](docs/ADDING_A_HERO.md) |
| `data/` | Your local data, not committed: `fights/` downloaded fights, `scripts/` replay scripts, `reviews/` all pages and results, `results/` finished sims, `apls/` saved action lists, `dungeons/` saved dungeons, `profiles/` `.simc` profiles, `settings.json`; for the web app, `visitors/` each visitor's sims, reviews, action lists and dungeons, and `keys/` the visitor cookie keys |

Game values are constants at the top of each class. Rime (`src/FellowshipSim.Core/Heroes/Rime`) is the complete
example; its `NOTES.md` lists what the combat logs showed.
