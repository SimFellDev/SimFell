# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

SimFell is a combat simulator for *Fellowship* (.NET 10, C# preview language version). It checks itself against
FellowshipLogs combat logs. The heroes are Rime, Ardeos, Gunde, Mara, Tariq and Elarion. `README.md` has setup and use. `docs/ADDING_A_HERO.md` has the engine
layout, the hook tables and the conventions for heroes, abilities, talents and gear. Read it before adding or changing
game content.

## Commands

```
# the solution (the desktop app needs the maui workload)
dotnet build -c Release

# the web app: engine and FellowshipSim.Service (http://simfell.dev.localhost:5000)
aspire run

# the desktop app on Windows
dotnet run --project src/FellowshipSim.Desktop -c Release -f net10.0-windows10.0.19041.0

# a sim from the command line
dotnet run --project src/FellowshipSim.Cli -c Release -- --profile data/profiles/Rime/PROFILE.simc --iterations 1000 --threads 8

# all tests (also replays every fight in data/fights and rewrites its review page)
dotnet test -c Release

# one test class or method
dotnet test tests/FellowshipSim.Tests -c Release --filter "FullyQualifiedName~AplParserTests"

# one hero's current-patch fights from FellowshipLogs into data/fights (credentials from the environment or the simfell user secrets)
dotnet run tools/fetch-fights.cs -- --hero Rime --count 15

# one hero's accuracy over those fights, saved as data/accuracy/<Hero>/<label>.txt (docs/REPLAY_ACCURACY.md)
ACCURACY_HERO=Rime ACCURACY_LABEL=baseline dotnet test tests/FellowshipSim.Tests -c Release --filter HeroAccuracyTests

# free dice over downloaded fights (FREEDICE_REVIEW=all or one review ID; FREEDICE_SEED, default 1)
FREEDICE_REVIEW=all FREEDICE_ITERATIONS=1000 dotnet test tests/FellowshipSim.Tests -c Release --filter FreeDice
```

The AppHost starts its projects from `bin/Debug`, so `aspire run` or `dotnet run --project src/FellowshipSim.AppHost`
needs a Debug build.

Tests are xUnit. Replay and free-dice tests do nothing without fights in `data/fights` (downloaded through the web app
or the desktop app).

## Architecture

- **FellowshipSim.Core** is the simulation and has no reference to logs or replays. `Engine/SimulationKernel` is the
  event queue; `SimulationContext` is the state for one run; `MonteCarloSimulator` runs iterations. Every random
  outcome (crit, proc, spread, travel time), each hit's damage (`Land`) and each periodic tick's time with a damage log ID
  (`TickAt`) go through `IOutcomeSource`. `RandomOutcomeSource` rolls dice;
  `LogOutcomeSource` (exact replay) and `FreeDiceOutcomeSource` (free dice) in FellowshipSim.Engine are the other two.
  `Import/SimcProfile` reads and writes `.simc` profiles.
- **Catalog** (`Core/Catalog.cs`) finds heroes, talents, traits, blessings, sets, legendaries and weapons by reflection
  over their ID constants. A new class with its ID constant and a parameterless constructor needs no registration.
- **Talents** attach to the hero in `Bind` through events and multiplier lists on `Hero` and `Ability`. Abilities never
  check for talents.
- **APL** (`Core/APL`) is a simc-style action-list grammar parsed with Superpower, plus a formatter.
- **FellowshipSim.Engine** is a class library. `ISimEngine` is every call a frontend makes: heroes, action-list
  vocabulary, checks, parse and write, reviews and sims. `LocalSimEngine` runs them in-process. Every argument and result
  round-trips through JSON (`SimEngineContractTests`). `Replay/` has replay-script building from a logged fight, exact
  replay, free dice, their HTML review pages, and everything a replay needs to read a log (`LogResources`,
  `ProcEvidence`, `ClassTuningHistory`, `PartyBuffs`); none of it is in Core. `Jobs/SimJobQueue` runs sims and reviews
  as jobs with progress, a few at once, the rest queued.
- **FellowshipSim.EngineServer** is `LocalSimEngine` over HTTP. A sim streams NDJSON (`SimStreamLine`). Every request
  needs the bearer `EngineKey`; AppHost generates it per run and gives it to FellowshipSim.Service alone.
- **FellowshipSim.UI** (Razor class library) has every page, component and style, and the app services: `ReplayLibrary`
  access, FellowshipLogs settings and report lookup, saved action lists (`ActionListStore`), finished sims
  (`SimResultLibrary`), each visitor's reviews (`VisitorReviews`), review jobs and the job queue. Every job has an owner,
  and `SimJobQueue` lists, reads, cancels and removes a job for its owner alone; components reach the queue only through
  `VisitorJobs`, the queue as one `Visitor` sees it. A web visitor is one browser (a cookie, `VisitorIdentity` in the
  Service), and their files are under `visitors/<id>/` in the `IBlobStore` (`DiskBlobStore` in the data folder). The
  desktop user is `Visitor.DataFolderOwner`: their files are directly in the data folder (`results/`, `apls/`, every
  review), and only they save FellowshipLogs credentials. A web visitor reads the shared `data/apls` lists without
  changing them. A job's work outlives its circuit, so it captures singletons and values only. `AddSimFellUI` registers
  them with each page's state, scoped to the circuit or window so a page keeps its input while another shows. The host
  registers `ISimEngine`, `IReviewPages`, the `Visitor` and its shell: `AddSimFellWebShell` (`Routes` with `Layout/MainLayout`, each page at its
  own address) or `AddSimFellDesktopShell` (`Shell/DesktopShell`, a tab per page, job and review page, with ‹ › and an
  open list when the result tabs overflow). Both frames have `Jobs/ResultsSidebar`: the queue with live progress, then
  the saved sims and reviews. `Shell/AppPage` lists the menu pages; their routes come from each page's `@page`. Every
  link inside the app goes through `IAppViews`, since the desktop app has no router. Pages run in Interactive Server and in BlazorWebView, so no component uses `HttpContext` or static server
  rendering. The action-list editor is in `Apl/`, with JS only for caret, drag and stored-mode work (`wwwroot/apl`);
  `wwwroot/shell` scrolls the desktop result tabs. Every tooltip is the app's own: `data-tooltip="..."` for plain text,
  `Shared/Tooltip` for markup, both shown by `wwwroot/tooltip` in each frame's `Shared/TooltipLayer`; no element has a
  `title` (`TooltipTests`).
- **FellowshipSim.Service** is the web app: a Blazor Web App (Interactive Server) hosting FellowshipSim.UI. Its
  `RemoteSimEngine` calls the engine server. It serves a review's files at `/reviews` to the visitor who ran it alone.
  Its FellowshipLogs credentials come from the host's environment, the `simfell` user secrets in development, or `data/settings.json`.
- **FellowshipSim.Desktop** is a .NET MAUI Blazor Hybrid app (`net10.0-windows10.0.19041.0`, `net10.0-maccatalyst`)
  hosting FellowshipSim.UI, with `LocalSimEngine` in-process and the data folder in local application data. Review
  pages come from a WebView2 virtual host on Windows and a URL scheme handler on Mac Catalyst.
- **FellowshipSim.Cli** is `simfell`: one `LocalSimEngine.SimulateAsync` of a `.simc` profile, result on stdout,
  progress on stderr.
- **FellowshipSim.Logs** is the FellowshipLogs client and the data folder's fights and reviews. It has no reference to
  the engine.
- Each component has its styles in `X.razor.css` beside `X.razor`. `wwwroot/css/app.css` in FellowshipSim.UI has
  colours, fonts and element defaults; `wwwroot/css/design-system.css` has classes several components share.

The desktop app and the CLI run fights in-process, so they copy the engine server's runtime options: `CallCountingDelayMs=0`, server GC
on and concurrent GC off (Windows only on the desktop app, since Mac Catalyst has no server GC). Without them fights take
about 2.5 times as long.

`data/` is local and not committed: `fights/`, `scripts/`, `reviews/`, `results/`, `apls/`, `profiles/`, `settings.json`,
and for the web app `visitors/` and `keys/` (the visitor cookie keys).

## Conventions

- One class per file, named after the class.
- Game values are public constants at the top of the class. Nothing reads game data at runtime. Each value's comment
  names its source (tooltip text, game data record or combat logs); `UNVERIFIED` marks a value no log has confirmed.
- Combat log IDs are game IDs plus an offset: effects + 1,000,000, talents + 2,000,000, traits + 3,000,000
  (`Domain/LogIds.cs`). Logs match blessings by name.
- Each hero's `Heroes/<Hero>/NOTES.md` lists the mechanics the combat logs showed for that hero.
- The engine models one game patch, `GamePatch.Current` (`FellowshipSim.Logs/GamePatch.cs`): the day it went live and
  its combat-log format version (the report's `logVersion`). A log from an earlier day or with a lower `logVersion` is
  flagged as old patch on import and in the reviews, and still runs. When a patch changes a mechanic, the engine takes
  the new one and drops the old: no code, values or name conversions for earlier patches. A new patch moves
  `GamePatch.Current`.
