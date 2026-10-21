# SimFell

A combat simulator for *Fellowship* (C#, .NET 10). It models heroes' abilities, talents and gear, and checks itself
against real combat logs from [FellowshipLogs](https://www.fellowshiplogs.com).

Supported heroes: **Rime**.

## You need

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A FellowshipLogs API client ID and secret (from your FellowshipLogs account), to download fights

## Build

```
dotnet build -c Release
```

## Run the web page (download and replay fights)

```
dotnet run -c Release --project src/FellowshipSim.Server
```

Open the address it prints (e.g. `http://localhost:5000`), then:

1. Enter your FellowshipLogs client ID and secret (saved in `data/settings.json`, which is not committed).
2. Paste a FellowshipLogs report link and load it.
3. Click a player. The fight is downloaded, replayed, and shows up in the review list.

Or pass the credentials as environment variables instead:

```
# PowerShell
$env:FELLOWSHIPLOGS_CLIENT_ID = "..."; $env:FELLOWSHIPLOGS_CLIENT_SECRET = "..."
# bash
export FELLOWSHIPLOGS_CLIENT_ID=... FELLOWSHIPLOGS_CLIENT_SECRET=...
```

## Edit action lists

The **Action lists** section of the web page edits an action list without writing its text. Each action has an
ability, a condition, a target and a comment; drag the handle or use the arrows to change the order. Each name in a
condition has a colour for its kind (buff, debuff, cooldown, resource), and a wavy underline marks a name the hero
does not have. Conditions in the editor chain with `and`, `or` and `not`; the text has `&`, `|` and `!`. A `&`, `|`
or `!` typed in the editor becomes its word when you leave the field. Parentheses stay as written. **Insert** adds a buff, debuff,
cooldown, resource or fight value, joined with `and` or `or` after a value. **Action list text** has the text of
the list. Text pasted there loads into the editor. **Save** writes `data/apls/<hero>/<name>.simc`.

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

Free dice runs from the tests, over fights you have already downloaded with the web page:

```
# every downloaded fight, 1000 runs each (about 1.5 minutes)
FREEDICE_REVIEW=all FREEDICE_ITERATIONS=1000 dotnet test tests/FellowshipSim.Tests -c Release --filter FreeDice

# one fight (the review ID from data/reviews, e.g. yMAZVJL9hTpFjqbC_fight7_p1)
FREEDICE_REVIEW=yMAZVJL9hTpFjqbC_fight7_p1 FREEDICE_ITERATIONS=1000 dotnet test tests/FellowshipSim.Tests -c Release --filter FreeDice
```

In PowerShell set the variables first: `$env:FREEDICE_REVIEW = "all"; $env:FREEDICE_ITERATIONS = "1000"`.
`FREEDICE_SEED` sets the first seed (default 1, so runs repeat exactly; `random` for a new one).
Refresh the web page afterwards to see the new numbers.

## Tests

```
dotnet test -c Release
```

Runs the engine tests and replays every downloaded fight, rewriting its review page.

## Where things are

| Folder | What's there |
|---|---|
| `src/FellowshipSim.Core` | The engine: simulation, heroes, talents, gear. Knows nothing about logs |
| `src/FellowshipSim.Logs` | FellowshipLogs client, turning a logged fight into a replay script, exact replay and free dice, their pages |
| `src/FellowshipSim.Server` | Local web server (`wwwroot` is the page) |
| `tests/FellowshipSim.Tests` | Tests, including the free-dice runner |
| `docs/` | [Adding a hero](docs/ADDING_A_HERO.md) |
| `data/` | Your local data, not committed: `fights/` downloaded fights, `scripts/` replay scripts, `reviews/` all pages and results, `apls/` saved action lists, `settings.json` |

Game values are constants at the top of each class. Rime (`src/FellowshipSim.Core/Heroes/Rime`) is the complete
example; its `NOTES.md` lists what the combat logs showed.
