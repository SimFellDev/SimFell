# Sim a profile

A profile is a `.simc` file: the hero, stats, talents, traits, blessings, gear and its `actions` lines, which are the
action list. Keep profiles in `data/profiles/<Hero>/`.

A sim's profile also has the sim settings: `iterations`, `target_error`, `threads`, `seed`, `duration` and `targets` (training
dummies), `length_variance` (a fraction), `health_follows_time`, `boss`, `starting_spirit`,
`reaction_delays` and `proc_reaction` (seconds). `target_error` is SimulationCraft's: the sim ends once the margin of
error of the mean DPS at 95% confidence is below that percent of the mean, checked from 100 fights on, and `iterations`
is the most fights (1,000,000 without an `iterations` line). A dungeon fight has a dungeon's lines in place of `duration` and `targets`. The lines are in any
order. The last line of a setting is the setting, and a `dungeon=` line starts a dungeon that replaces the one before.
Every option of the **Sim** page is a line of this text, and a sim's job is the text alone.

The **Sim** page loads a profile file or pasted text, sets iterations and the fight, and starts the sim.
The sim's page (a tab in the desktop app) shows its progress, then its result: mean, minimum and maximum DPS, the
standard deviation, the margin of error, the fights by DPS, and DPS for each ability. **Save as HTML** saves the result as an HTML file. **Advanced**, at the
end, has the sim's profile. **Load** puts its build, action list and settings in the Sim page, as a compare run's
**Load** does.

## Fights

The **Modes** tab has two kinds of fight:

- **Training dummies** (the default): a number of training dummies for the whole duration. **The dummies are bosses**
  sets whether boss-only effects (Ruby's Blessing of the Conqueror) count.
- **Dungeon**: the pulls of a dungeon in turn, up to the dungeon's time limit. Each enemy's health falls from full to
  none over its pull's duration, whatever the hero does, and the enemy dies at the duration's end. A hero who does
  nothing still ends each pull at its duration. Between pulls is downtime, with no enemies and no actions. The fight's
  DPS is over its whole length, downtime included. The result adds the DPS to bosses, the party DPS (the health the
  enemies lost each second, also a line of the DPS chart) and the hero's share of the group's damage: the hero's damage
  to each enemy, up to its health, over the party's. The Dungeon card starts with the built-in `Default` dungeon: Cithrel's Fall's
  creatures in four trash pulls and two bosses, Cithrel the final boss.

**Proc reaction (ms)** in the **Fight settings** card is the time a proc's gain takes to reach the action list: 300 ms by
default, as SimulationCraft's player `reaction_mean`. Until it passes, `buff.<proc>` reads the proc as it was before the gain:
down for a new proc, the stacks before a new stack, and the time left before a refresh. 0 shows each gain at once. The
command line's `--proc-reaction <ms>` is the same setting.

The **Dungeon** card has a comment, the time limit, the difficulty and the pulls. Each pull has a
comment and a name in its head, and opens to its name, its start (after downtime, or chained to another pull's end), its
duration, the final boss, another pull it takes in with that pull's trigger, and its enemy groups. A group has a
comment, a count (trash), a name, Spirit Points, hit points, priority, and an interrupt ability with its cooldown and cast
time. Empty Spirit Points and hit points are the game data values of the creature with the group's name. A new pull's name is the lowest whole number no pull has. Renaming a pull renames it in every `chain_with` and
`pull_with`. **Save** writes `data/dungeons/<name>.simc` in the desktop app and `data/visitors/<visitor id>/dungeons/` in
the web app.

The Dungeon card's toolbar has **Revert**, **Save**, **Duplicate**, **Import**, **Export** and **Delete**. **Import** takes a
FellowshipLogs fight link, a `.simc` file (the upload icon) or pasted dungeon text. A fight link's import reads the
fight's enemy deaths and one player's damage events, dealt and taken: the fight's tank's, or with no tank, a damage
dealer's (`Engine/Replay/DungeonReader`):

- The difficulty is the Eternal level of the fight's `difficultyLevel` (50 is Eternal 30).
- The enemies are the enemies that player hit or was hit by, and the enemies dying without either. Each enemy's name, hit
  points, Spirit Points and boss rank are its creature's in the game data; an enemy of no creature is left out and named
  in the dungeon's comment.
- The pulls are the dungeon's packs: its creature placements in the Codex's data export, a patrol's placements together
  and every other placement with those within 1,200 units of it. Each enemy is in the pack of the nearest placement of its
  creature to its first contact, or of a pack met in the 30 seconds before. An enemy with neither is a spawn, a pull of
  its own with the spawns of its creature met within 3 seconds of it.
- A pull's duration runs from its first contact to its last death. Its downtime is the time since the pulls before it
  ended; a pull starting before that end joins the pull before it through `pull_with` at its log offset.
- Each enemy is in combat from its own first contact (`first_seen`) to its own death (`dies`), or from its pull's start
  when it dies without contact. A pull's enemies of one creature within 2 seconds of each other in both are one line with
  the mean of each, and each boss has a line.
- The affixes are the affixes of the fight's `dungeonstart` event that the engine models. Each Anomalous Orb line has
  `priority=1`.

**Export** shows the dungeon as text, with **Copy** and **Download**:

```
# Cithrel's Fall, two trash pulls before the first boss.
dungeon=Default
difficulty=10
time_limit=1800
affixes=anomalous_orbs,storm_shield
# Imps and their captain.
pull.1=/duration=40
pull.1+=/count=5,name=Imp,spirit_points=3,hit_points=7857
# Big Cast is interruptible.
pull.1+=/count=1,name="Ogre Captain",spirit_points=10,priority=1,hit_points=11546,interrupt_ability="Big Cast",cooldown=20,cast_time=2.5
pull.2=/count=4,name=Ghoul,hit_points=5000,dies=30
pull.2+=/count=2,name=Ghoul,hit_points=5000,first_seen=12.5
pull.2+=/duration=45,downtime=20
pull.3=/count=3,name=Bat,spirit_points=2,hit_points=3374
pull.3+=/duration=30,chain_with=2
pull.last_boss=/duration=90,final=1,downtime=30,pull_with=wisps,offset_pct=50
pull.last_boss+=/boss.1,name="Ancient Koros"
pull.wisps=/count=2,name=Wisp,hit_points=15145,duration=20
```

A `#` line is a comment. A comment above a `pull.X+=/` enemy group line belongs to that group, a comment above any other
pull line or after the last pull belongs to the pull, and any other comment belongs to the dungeon. A pull whose first
group has a comment starts with its options line, or with an empty `pull.X=/` line.

`difficulty` is an Eternal level, 0 for Base. Each difficulty multiplies creature health and damage by the game's scale
for it: 1.31 at Base, 58.26 at Eternal 10 and 351.49 at Eternal 32 for health.

`affixes` names the run's dungeon affixes, separated by commas. A text without it has none. The dungeon editor has a
checkbox for each.

- `anomalous_orbs` (Anomalous Orbs): each enemy line named `Anomalous Orb` is an orb. A text with no such line has three
  orbs 128 to 148 seconds into the run, then every 128 to 148 seconds of combat, each in combat for 13 seconds. An orb is a priority target
  (`target.is_priority`) with 0 Spirit Points. It casts Detonate (`target.casting`) from entering combat. The party stops
  each cast after 2.15 seconds, and the orb casts again 9 seconds later, or 12 seconds after an interrupt of the hero's.
  An orb takes 4 times the damage while it is not casting. Its death gives the hero a stack of Anomalous Residue: 480
  power of the orb's gem colour for 60 seconds, 3 stacks of a colour at most.
- `storm_shield` (Storm Shield): every 59 to 72 seconds of the run, the hero has Storm Shield for 8 seconds, then
  Storm's Fury for 20 seconds: 20% crit chance.

A wave of orbs or a charge due in a boss fight comes 12.4 seconds later, and a boss's end drops it for a new wait.

`pull.X=/` starts pull X and `pull.X+=/` adds a line to it. A pull name has letters, digits and `_`. A line is an enemy
group (`count=`), a boss (`boss.N`; several make a council) or the pull's options. Group values: `count`, `name` (in
quotes when it has a comma), `spirit_points` (each enemy's), `priority=1`, `hit_points` (the creature's health, before
the difficulty scale), `interrupt_ability`, `cooldown` and `cast_time` (2.5 s unless given; the first cast is one
cooldown after the enemy arrives), `first_seen` and `dies`. A group without `spirit_points` or `hit_points` takes the
value of the game data's creature of its name, so `name="Ancient Koros"` alone is the boss with its game data health and
Spirit Points.

A group's enemies enter combat `first_seen` seconds after their pull starts (0 unless given) and die `dies` seconds after
it starts (the pull's `duration` unless given). Each enemy's health falls from full to none between the two. `dies` is
more than `first_seen` and at most the pull's `duration`. In the example, pull 2's first four Ghouls die 30 s into the
pull, and two more enter combat at 12.5 s and die at the pull's 45 s. While a pull has no enemy in combat and one still to
enter, the fight is as in downtime: the hero takes no action. `active_enemies` counts the enemies in combat.

Pull options:

- `duration`: seconds from the pull's start to its enemies' deaths. Every pull has one.
- `downtime`: seconds before the pull starts, after the pull before it ends.
- `chain_with=X`: the pull starts as pull X ends, with no downtime.
- `pull_with=X`: pull X's enemies join this pull, at its start or at one trigger: `offset_ms` after the start,
  `offset_pct` (the pull's average health %), `after_deaths` (the pull's own enemies dead) or `after_deaths_total` (all
  enemies of the combined pull dead). A death count is a number or a percent (`50%`).
- `final=1`: the pull's boss is the final boss.

Pulls run in the order of their `pull.X=/` lines. A pull taken in by another runs with it, and a chained pull runs as the
pull it names ends. The fight's length variance varies each `duration`, `first_seen` and `dies`.

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
| `target.time_to_die` | seconds until the target dies, from its health loss so far, its group's `dies` or pull duration, or the training dummy fight's end |
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

## Sim from the command line

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
