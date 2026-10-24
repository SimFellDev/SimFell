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
- The [Aspire CLI](https://aspire.dev), to run the web app
- The .NET MAUI workload (`dotnet workload install maui`), to build the desktop app
- A FellowshipLogs API client ID and secret (from your FellowshipLogs account), to download fights

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

Without either, the app reads `data/settings.json`. **Setup** shows whether API access is set up.

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
**Setup** saves your FellowshipLogs client ID and secret in its `settings.json`.

The desktop app has the same pages as the web app, each as a tab. Starting a sim opens its tab, which shows its progress
and then its result. A replay started from **Import** opens its review page in a tab when it finishes. Picking a result
from the **Results** sidebar also opens its tab. You can close these tabs; the page tabs stay. A tab keeps what you
entered while another tab shows.

When the result tabs don't fit, ‹ and › scroll them, as does the mouse wheel over them. The **N open** list beside them
names every open result tab, selects or closes one, and closes them all.

## Sim a profile

A profile is a `.simc` file: the hero, stats, talents, traits, blessings, gear and its `actions` lines, which are the
action list. Keep profiles in `data/profiles/<Hero>/`.

The **Sim** page loads a profile file or pasted text, sets iterations, fight duration and targets, and starts the sim.
The sim's page (a tab in the desktop app) shows its progress, then its result: mean, minimum and maximum DPS, the
standard deviation, and DPS for each ability.

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
Its options are the following:

- `--profile <file>`: the profile to sim (required)
- `--iterations <n>`: fights (default 100)
- `--duration <seconds>`: length of each fight (default 300)
- `--targets <n>`: targets in each fight (default 1)
- `--seed <n>`: seed of the first fight; fight i has seed + i (default 1)
- `--threads <n>`: most threads the fights use (default 80% of the logical processors)
- `--format <text|json>`: a text summary, or the `SimResult` record as JSON (default `text`)

## Edit action lists

The **Action lists** page edits an action list without writing its text. Each action has an
ability, a condition, a target and a comment; drag the handle or use the arrows to change the order. Each name in a
condition has a colour for its kind (buff, debuff, cooldown, resource), and a wavy underline marks a name the hero
does not have. Conditions in the editor chain with `and`, `or` and `not`; the text has `&`, `|` and `!`. A `&`, `|`
or `!` typed in the editor becomes its word when you leave the field. Parentheses stay as written. **Builder** shows a
condition as rows: each row has its `and` or `or`, its `not`, and a buff, debuff, cooldown, resource or fight value with
an optional comparison; **Group ( )** adds rows in parentheses. **Text** shows the condition as text. Typing a name, a `.`
or Ctrl+Space lists the names that fit; Enter or Tab writes the one picked. A condition with arithmetic has no rows, so
it shows as text. **Action list text** has the text of the list. Text pasted there loads into the editor. **Save** writes `data/apls/<hero>/<name>.simc` in the desktop app. In the web
app, **Save** writes one of your own lists in `data/visitors/<visitor id>/apls/`, and **Saved** also lists the shared
lists in `data/apls/`, marked "shared". A shared list loads into the editor, and saving it makes your own copy.

A target picks the enemy for the cast, written `target_if=` in the text:

- `target_if=min:debuff.<name>.remains`: the enemy with the lowest value (`max:` for the highest)
- `target_if=debuff.<name>.down`: the first enemy where it is true; the action is skipped when there is none

`target.*` and `debuff.*` in the action's `if` read the enemy picked.

Each decision starts at the default list (`actions+=/`). **Add action list** adds a named list (`actions.aoe+=/`), and an
action's **Type** runs it:

- `run_action_list,name=aoe`: `aoe` runs, and the actions after this one do not
- `call_action_list,name=aoe`: `aoe` runs, then the next action when nothing in `aoe` is cast

Both take an `if` like any other action. A list the decision is already in is skipped. A list named by
`run_action_list` has a warning until it has an action that is always ready: an ability with no cooldown and no cast
requirement (Frost Bolt for Rime), with no condition or target.

## The two checks

- **Exact replay** (the review pages): the log decides every crit, proc and target, and each engine hit is compared
  with the log's. Good for finding which mechanic is wrong.
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
| `src/FellowshipSim.Cli` | `simfell`, the command-line sim |
| `tests/FellowshipSim.Tests` | Tests, including the free-dice runner |
| `docs/` | [Adding a hero](docs/ADDING_A_HERO.md) |
| `data/` | Your local data, not committed: `fights/` downloaded fights, `scripts/` replay scripts, `reviews/` all pages and results, `results/` finished sims, `apls/` saved action lists, `profiles/` `.simc` profiles, `settings.json`; for the web app, `visitors/` each visitor's sims, reviews and action lists, and `keys/` the visitor cookie keys |

Game values are constants at the top of each class. Rime (`src/FellowshipSim.Core/Heroes/Rime`) is the complete
example; its `NOTES.md` lists what the combat logs showed.
