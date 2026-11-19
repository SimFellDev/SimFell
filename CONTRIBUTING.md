# Contribute to SimFell

SimFell takes contributions as pull requests to the `main` branch of
[SimFellDev/SimFell](https://github.com/SimFellDev/SimFell). This page has the setup, the conventions and the
requirements of a pull request.

Every contribution follows the [AI usage policy](AI_POLICY.md) and is under the project's license, the
[GNU General Public License v3.0](LICENSE).

## Report a problem

Open an [issue](https://github.com/SimFellDev/SimFell/issues). For a wrong sim or replay result, include the following:

- The hero and the `.simc` profile.
- The FellowshipLogs report link, the fight and the player.
- The value SimFell gives and the value the game or the log gives.

## Set up the repository

1. Fork [SimFellDev/SimFell](https://github.com/SimFellDev/SimFell) and clone your fork.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org) with npm.
3. Run the tests:

   ```
   dotnet test tests/FellowshipSim.Tests -c Release
   ```

For each app's requirements and run commands, see [Development](docs/DEVELOPMENT.md).

## Follow the conventions

The following conventions apply to every change:

- Each class has its own file, named after the class.
- Game values are public constants at the top of the class. Each value's comment names its source: the tooltip text, the
  game data record or the combat logs. `UNVERIFIED` marks a value no log has confirmed.
- The engine models one game patch, `GamePatch.Current`. A change for a later patch replaces the earlier patch's
  mechanic.
- Each hero's `Heroes/<Hero>/NOTES.md` lists the mechanics the combat logs showed for that hero. A change to a mechanic
  updates it.
- The classes in `src/FellowshipSim.Core/Generated/Codex/` are generated. To change one, change its generator or
  regenerate the folder with `tools/regenerate-codex.sh`, then commit the folder.
- A test of a game data value has the value as a number.
- Each component has its styles in `X.razor.css` beside `X.razor`.

For the engine's layout and the hooks of heroes, abilities, talents and gear, see
[Adding a hero](docs/ADDING_A_HERO.md). For the accuracy targets and the rules of evidence, see
[Replay accuracy](docs/REPLAY_ACCURACY.md).

## Open a pull request

1. Create a branch from `main` for one subject.
2. Run every test:

   ```
   dotnet test -c Release
   ```

3. Push the branch to your fork and open a pull request to `main` of SimFellDev/SimFell.
4. In the description, state the subject and the source of each game value or mechanic the change adds. For an accuracy
   change, give the `HeroAccuracyTests` numbers before and after it.

The **Tests** workflow runs on each pull request. Its `dotnet test` job runs every test, and its
`Codex generated files` job checks the committed generated classes against the Codex's data export. A maintainer reviews
the pull request and squash-merges it.
