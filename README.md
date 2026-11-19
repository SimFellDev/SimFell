# SimFell

SimFell is a combat simulator for *Fellowship*, written in C# on .NET 10. It models each hero's abilities, talents and
gear, and checks its results against combat logs from [FellowshipLogs](https://www.fellowshiplogs.com).

## Use SimFell

SimFell runs in the following ways:

- **Browser app**: open [simfelldev.github.io](https://simfelldev.github.io). Every sim and replay runs on your own
  computer.
- **Local app**: download the zip for your system from the
  [latest release](https://github.com/SimFellDev/SimFell-Releases/releases/latest), extract the `SimFell` folder, and
  start `SimFell.exe` (Windows), `SimFell.command` (macOS) or `simfell.sh` (Linux). SimFell opens in your browser.
- **VS Code extension**: install `simfell-simc-<platform>.vsix` from the same release. It colours, checks and completes
  `.simc` files.
- **`simfell`**: sim a `.simc` profile from the command line, from a build of this repository.

## Features

- **Sim**: fights a `.simc` profile many times against training dummies or the pulls of a dungeon, and gives the DPS
  with its margin of error, the fights by DPS, and the DPS of each ability.
- **Log Replay**: replays a FellowshipLogs fight with the log's own crits, procs and targets, and lists each logged
  value outside the range the engine computes for it.
- **APL Editor**: edits an action list as rows or as text, with colours, checks and completion.
- **APL Debugger**: fights one fight and shows every decision of the action list, line by line.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org) with npm.

Run the tests:

```
dotnet test tests/FellowshipSim.Tests -c Release
```

Run the browser app:

```
dotnet run --project src/FellowshipSim.Web
```

Sim a profile from the command line:

```
dotnet run --project src/FellowshipSim.Cli -c Release -- --profile PROFILE_FILE --iterations 1000
```

Replace `PROFILE_FILE` with the path of a `.simc` profile.

For the web app, the desktop app, the FellowshipLogs sign-in and the game data generators, see
[Development](docs/DEVELOPMENT.md).

## Documentation

The `docs/` folder has the following pages:

- [Sim a profile](docs/SIMULATING.md): profiles, sim settings, fights, the dungeon text and the command line.
- [Action lists](docs/ACTION_LISTS.md): the editor, the action list text and the debugger.
- [Edit .simc files in VS Code](docs/VSCODE_EXTENSION.md): the extension and its packaging.
- [Development](docs/DEVELOPMENT.md): requirements, build, each app's run commands, tests and folders.
- [Adding a hero](docs/ADDING_A_HERO.md): the engine's layout, hooks and conventions for game content.
- [Replay accuracy](docs/REPLAY_ACCURACY.md): the accuracy targets, the measure and the rules of evidence.

## Contribute

SimFell takes pull requests. For the setup, the conventions and the requirements of a pull request, see
[Contribute to SimFell](CONTRIBUTING.md). For the rules on AI tools, see the [AI usage policy](AI_POLICY.md).

## License

SimFell is free software under the [GNU Affero General Public License v3.0](LICENSE)

*Fellowship*, its game data, names and icons belong to Chief Rebel. SimFell is a fan project with no affiliation to Chief Rebel or FellowshipLogs.

## Acknowledgements

- [SimulationCraft](https://github.com/simulationcraft/simc): the model SimFell's engine, profiles and action lists
  follow.
- [FellowshipLogs](https://www.fellowshiplogs.com): the combat logs SimFell replays.
- [Fellowship Codex](https://codex.fellowshipanalyzer.com): the game data export SimFell's game data classes are
  generated from.
