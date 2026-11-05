// One sim worker's memory and time in Node: the published browser app's .NET runtime (its _framework), started as
// sim/worker.js starts it, running SimWorker's calls. After the start and after each workload it prints the worker's
// WebAssembly memory, the process's resident memory, the managed heap after a full collection, the collections so far and
// the workload's time. A runtime's memory never shrinks, so each line's WebAssembly memory is the worker's peak so far.
//
//   node benchmarks/wasm/worker-memory.mjs <publish folder> <workload> [...]
//
// Workloads:
//   sim:<fights>[:<targets>]      a Rime sim of 300 s fights, every chunk on this worker, then combined
//   dungeon:<fights>              the same in the default dungeon
//   review:<raw fight>:<player>   a replay of a fight the browser keeps (SimWorker.HoldFight, then SimWorker.Review)
//   build:<raw fight>:<player>    the player's replay script built alone (WorkerMeasures.BuildScript)
//   download:<raw fight>:<player> a first replay (SimWorker.ReviewFight): the fight downloaded from FellowshipLogs, whose
//                                 answers come from the raw fight in pages of 10,000 events, then replayed
//
// MONO_GC_PARAMS in the environment reaches the worker's GC.
import { readFileSync, readdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { join, resolve } from 'node:path';

const [publish, ...workloads] = process.argv.slice(2);
if (!publish) throw new Error('Usage: worker-memory.mjs <publish folder> <workload> [...]');
const framework = join(resolve(publish), 'wwwroot', '_framework');
const repo = resolve(import.meta.dirname, '..', '..');

// dotnet.js has the build's fingerprint in its name (the page finds it through its import map).
const dotnetJs = readdirSync(framework).find(f => /^dotnet(\.[a-z0-9]+)?\.js$/.test(f));
const { dotnet } = await import(pathToFileURL(join(framework, dotnetJs)).href);
const began = performance.now();
if (process.env.MONO_GC_PARAMS) dotnet.withEnvironmentVariable('MONO_GC_PARAMS', process.env.MONO_GC_PARAMS);
const runtime = await dotnet.create();
runtime.setModuleImports('simWorker', {
  progress: () => { },
  bytes: bytes => bytes,
  // As worker.js: the view is copied before the save.
  saveFight: async (reportCode, fightId, playerId, view) => {
    view.slice();
    view.dispose();
  },
});
// FellowshipLogs' answers for a first replay (serve).
let fellowshipLogs;
runtime.setModuleImports('workerMeasures', { answer: query => fellowshipLogs(JSON.parse(query)) });
const exports = (await runtime.getAssemblyExports('FellowshipSim.Web')).FellowshipSim.Web;
const worker = exports.SimWorker;
const measures = exports.WorkerMeasures;

const mb = bytes => (bytes / 1048576).toFixed(1);
function line(label, ms) {
  const gc = JSON.parse(measures.Memory());
  console.log(`${label.padEnd(24)} wasm ${mb(runtime.Module.HEAPU8.length).padStart(7)} MB   ` +
    `rss ${mb(process.memoryUsage.rss()).padStart(7)} MB   managed ${mb(gc.heap).padStart(7)} MB   ` +
    `gc ${String(gc.collections).padStart(5)}   ${(ms / 1000).toFixed(2).padStart(7)} s`);
}
line('start', performance.now() - began);

const build = JSON.parse(readFileSync(join(repo, 'tests/FellowshipSim.Tests/Fixtures/rime_character.json'), 'utf8'));
const actionList = readFileSync(join(repo, 'benchmarks/FellowshipSim.Benchmarks/Fixtures/rime.simc'), 'utf8');
const profileJson = JSON.stringify({ build, actionList });
let run = 0;

// FellowshipLogs for one raw fight: its report and fight, and each events query's events from a time in pages of 10,000,
// each page's next timestamp that of the event after it (so events at that time are in both pages, as FellowshipLogs has
// them).
function serve(raw) {
  const fight = JSON.parse(raw);
  const enemies = new Set(fight.master_data.actors.filter(a => a.type === 'NPC').map(a => a.id));
  const attacks = new Set(fight.master_data.abilities.filter(a => a.name === 'Attack').map(a => a.gameID));
  fellowshipLogs = body => {
    let report;
    if (body.query.includes('events(')) {
      const from = body.variables.startTime;
      const events = fight.events.filter(selects(enemies, attacks, body.variables));
      let first = events.findIndex(e => e.timestamp >= from);
      if (first < 0) first = events.length;
      const page = events.slice(first, first + 10000);
      report = { events: { data: page, nextPageTimestamp: events[first + 10000]?.timestamp ?? null } };
    } else {
      report = { startTime: fight.report_start_time, fights: [fight.fight], masterData: fight.master_data };
    }
    return JSON.stringify({ data: { reportData: { report } } });
  };
  return { reportCode: fight.report_code, fightId: fight.fight.id };
}

// The events of a fight an events query with these variables returns (FellowshipLogsFromFight in the tests): with a
// sourceID, the events with that actor as source or as target; Debuffs or Buffs of Enemies, those auras on enemies;
// Deaths of Enemies, enemies' deaths; Casts of Enemies, enemies' casts and begincasts except their melee swings (the
// ability Attack); a filterExpression of types (type = "a" or type = "b"), the events of those types;
// and otherwise every event.
function selects(enemies, attacks, { sourceID = 0, dataType = 'All', hostilityType = 'Friendlies', filterExpression = '' }) {
  const types = [...filterExpression.matchAll(/type = "([^"]+)"/g)].map(m => m[1]);
  return e => {
    const onEnemy = enemies.has(e.targetID);
    if (sourceID !== 0) return e.sourceID === sourceID || e.targetID === sourceID;
    if (types.length > 0) return types.includes(e.type);
    if (hostilityType === 'Enemies' && dataType === 'Debuffs') return /debuff/.test(e.type) && onEnemy;
    if (hostilityType === 'Enemies' && dataType === 'Buffs') return /buff/.test(e.type) && !/debuff/.test(e.type) && onEnemy;
    if (hostilityType === 'Enemies' && dataType === 'Deaths') return e.type === 'death' && onEnemy;
    if (hostilityType === 'Enemies' && dataType === 'Casts') {
      return (e.type === 'cast' || e.type === 'begincast') && enemies.has(e.sourceID) && !attacks.has(e.abilityGameID);
    }
    return true;
  };
}

// A replay's reply (ReviewReply.ToBytes): its head's JSON after the head's length. A refused replay throws its reason.
function failed(reply) {
  const length = new DataView(reply.buffer, reply.byteOffset).getInt32(0, true);
  const head = JSON.parse(new TextDecoder().decode(reply.subarray(4, 4 + length)));
  if (head.error) throw new Error(head.error);
}

for (const workload of workloads) {
  const [kind, ...args] = workload.split(':');
  // The player ID is after the path's last colon: a Windows path has a colon of its own.
  const path = args.slice(0, -1).join(':');
  const player = Number(args.at(-1));
  const started = performance.now();
  if (kind === 'sim' || kind === 'dungeon') {
    const settings = { iterations: Number(args[0] ?? 1000), durationSeconds: 300, targetCount: Number(args[1] ?? 1) };
    if (kind === 'dungeon') settings.dungeon = JSON.parse(measures.DefaultDungeon());
    const settingsJson = JSON.stringify(settings);
    const id = ++run;
    worker.Start(id, profileJson, settingsJson);
    const chunks = [];
    for (let c = 0; c < measures.ChunkCount(settingsJson); c++) chunks.push(worker.Chunk(id, c));
    worker.Finish(id);
    worker.Combine(profileJson, settingsJson, chunks);
  } else if (kind === 'review') {
    worker.HoldFight(readFileSync(path));
    failed(await worker.Review(player, 0));
  } else if (kind === 'build') {
    measures.BuildScript(readFileSync(path), player);
  } else if (kind === 'download') {
    const { reportCode, fightId } = serve(readFileSync(path, 'utf8'));
    failed(await measures.ReviewFight(reportCode, fightId, player));
  } else {
    throw new Error(`No workload named ${kind}.`);
  }
  line(kind === 'review' || kind === 'download' || kind === 'build' ? `${kind}:${player}` : workload, performance.now() - started);
}
process.exit(0);
