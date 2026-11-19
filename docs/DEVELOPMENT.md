# Development

This page has the requirements, the build, each app's run commands, the tests and the repository's folders.

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

The engine's game data (heroes, abilities, talents, gear items and powers, creatures, dungeons and the game icons) is
generated from the Codex's data export by `FellowshipSim.Codex.Generators`. The generated classes are committed in
`src/FellowshipSim.Core/Generated/Codex/`, one folder per generator, and the build compiles them, so a build needs no
export. A review shows every game data value a change touches.

The generators run in a build of their own, over the export in `codex-export/` (git ignores it). Run it after a new
datamine, a change to a generator, or a new or renamed `partial` class the generators write a half of, and commit the
folder:

```
tools/download-codex-export.sh
tools/regenerate-codex.sh
dotnet test tests/FellowshipSim.Tests -c Release
```

On Windows, run the scripts in Git Bash. `regenerate-codex.sh` writes the folder again and its `codex-export.sha256`, the
hash of the export the files are from. CI's Tests workflow checks the folder (`tools/check-codex-generated.sh`): with the
export of `codex-export.sha256`, the regenerated folder must be the committed one; a newer export gives a warning until
someone regenerates. Two branches regenerated from one export merge without a conflict in the folder, unless they change
one file. After such a merge, regenerate and commit the folder.

A test that checks a game data value has the value as a number. A new export with a different value fails that test,
and the test's new number is part of the commit.

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
2. Click a player. The fight is downloaded for that player and replayed as a job. When the job is done, its review
   page opens. The **Reviews** page lists your reviews.

## Run the desktop app

```
dotnet run --project src/FellowshipSim.Desktop -c Release -f net10.0-windows10.0.19041.0
```

On macOS, use the target framework `net10.0-maccatalyst`. Its data folder is `FellowshipSim` in your local application
data (`%LOCALAPPDATA%\FellowshipSim` on Windows). The data folder is yours alone: every review in it is listed.
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

## The two checks

- **Exact replay** (the review pages): every crit, proc, target, landing time, damage amount and periodic tick time is
  the log's, and each logged value is checked against the range the engine computes for it. A review page lists each
  value outside its range under one finding for its damage ID, resource, aura, ability or proc, ranked by the damage at
  stake, with examples and the engine's state at each of them. Good for finding which mechanic
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
| `src/FellowshipSim.LanguageServer` | The language server of `.simc` files: colours, checks and completion for action lists, characters, settings and dungeons over the Language Server Protocol |
| `src/FellowshipSim.VsCode` | The VS Code extension for `.simc` files, with the language server in it |
| `tests/FellowshipSim.Tests` | Tests, including the free-dice runner |
| `docs/` | [Sim a profile](SIMULATING.md), [Action lists](ACTION_LISTS.md), [Edit .simc files in VS Code](VSCODE_EXTENSION.md), [Adding a hero](ADDING_A_HERO.md), [Replay accuracy](REPLAY_ACCURACY.md) |
| `data/` | Your local data, not committed: `fights/` downloaded fights (one file per player), `scripts/` replay scripts, `reviews/` all pages and results, `results/` finished sims, `apls/` saved action lists, `dungeons/` saved dungeons, `profiles/` `.simc` profiles, `settings.json`; for the web app, `visitors/` each visitor's sims, reviews, action lists and dungeons, and `keys/` the visitor cookie keys |

Game values are constants at the top of each class. Rime (`src/FellowshipSim.Core/Heroes/Rime`) is the complete
example; its `NOTES.md` lists what the combat logs showed.
