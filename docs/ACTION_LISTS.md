# Action lists

This page has the **Action lists** page's editor, the action list text and the **APL Debugger**.

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
