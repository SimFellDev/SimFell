// Visual action-list editor: one section per action list, each with its actions in priority order. The server parses
// and writes the action-list text (api/apl), so the editor and the engine read the same grammar.

const apl = {
  hero: null,
  vocabulary: null,
  lists: [], // { name, actions: [{ key, ability, condition, targetSelection, targetExpression, comment, arguments, actionList }] }, the default list first
  open: new Set(), // keys of the actions with details shown
  checks: new Map(), // `${hero}\n${text}` -> check result
  nextKey: 1,
};

function newAction(fields = {}) {
  return { key: apl.nextKey++, ability: "", condition: null, targetSelection: null, targetExpression: null, comment: null, arguments: null, actionList: null, ...fields };
}

function debounce(fn, ms) {
  let timer = null;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

// ---- Expressions -------------------------------------------------------------------------

async function checkExpression(text) {
  const cacheKey = `${apl.hero}\n${text}`;
  if (!apl.checks.has(cacheKey)) {
    apl.checks.set(cacheKey, api(`/api/apl/heroes/${encodeURIComponent(apl.hero)}/check`, { method: "POST", body: JSON.stringify({ text }) }));
  }
  return apl.checks.get(cacheKey);
}

// The text as HTML, each token coloured by its kind. The character at a parse error is marked.
function highlight(text, check) {
  let html = "";
  let at = 0;
  const plain = (end) => {
    if (end <= at) return;
    const errorAt = check?.errorStart;
    if (errorAt != null && errorAt >= at && errorAt < end) {
      html += escapeHtml(text.slice(at, errorAt)) + `<span class="tok-error">${escapeHtml(text[errorAt])}</span>`;
      at = errorAt + 1;
    }
    html += escapeHtml(text.slice(at, end));
    at = end;
  };
  for (const token of check?.tokens ?? []) {
    plain(token.start);
    const errorClass = check.errorStart != null && check.errorStart >= token.start && check.errorStart < token.start + token.length ? " tok-error" : "";
    html += `<span class="tok-${token.kind}${errorClass}">${escapeHtml(text.slice(token.start, token.start + token.length))}</span>`;
    at = token.start + token.length;
  }
  plain(text.length);
  if (check?.errorStart != null && check.errorStart >= text.length) html += `<span class="tok-error"> </span>`;
  return html;
}

function problemsOf(check) {
  return [...new Set((check?.tokens ?? []).map((t) => t.problem).filter(Boolean))];
}

// A multi-line code input with its text coloured underneath; onChange gets the new text.
function codeField(value, placeholder, onChange) {
  const field = document.createElement("div");
  field.className = "code-field";
  field.innerHTML = `<pre aria-hidden="true"></pre><textarea rows="1" spellcheck="false" autocomplete="off"></textarea>`;
  const pre = field.querySelector("pre");
  const input = field.querySelector("textarea");
  input.placeholder = placeholder;
  input.value = value ?? "";

  const status = document.createElement("div");
  const wrapper = document.createElement("div");
  wrapper.append(field, status);

  const fit = () => {
    input.style.height = "auto";
    input.style.height = `${input.scrollHeight + 2}px`;
  };
  const render = async () => {
    const text = input.value;
    pre.innerHTML = escapeHtml(text);
    if (!text.trim()) {
      field.classList.remove("invalid");
      status.innerHTML = "";
      return;
    }
    const check = await checkExpression(text).catch(() => null);
    if (input.value !== text) return;
    pre.innerHTML = highlight(text, check);
    field.classList.toggle("invalid", !!check?.error);
    status.innerHTML =
      (check?.error ? `<p class="apl-field-error">${escapeHtml(check.error)}</p>` : "") +
      problemsOf(check).map((p) => `<p class="apl-field-warning">${escapeHtml(p)}</p>`).join("");
  };
  const renderSoon = debounce(render, 150);

  input.addEventListener("input", () => {
    pre.innerHTML = escapeHtml(input.value);
    fit();
    renderSoon();
    onChange(input.value);
  });
  // The editor writes 'and', 'or' and 'not': on leaving the field, '&', '|' and '!' become their words.
  input.addEventListener("change", async () => {
    const text = input.value;
    const check = await checkExpression(text).catch(() => null);
    if (input.value !== text || check?.inWords == null || check.inWords === text) return;
    input.value = check.inWords;
    input.dispatchEvent(new Event("input"));
  });
  // Enter would put a line break in an action line.
  input.addEventListener("keydown", (event) => {
    if (event.key === "Enter") event.preventDefault();
  });
  wrapper.input = input;
  wrapper.textBeforeCursor = () => input.value.slice(0, input.selectionStart ?? input.value.length);
  wrapper.insert = (snippet) => {
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;
    input.setRangeText(snippet, start, end, "end");
    input.dispatchEvent(new Event("input"));
    input.focus();
  };
  requestAnimationFrame(fit);
  render();
  return wrapper;
}

// True when the text ends with a value, so the next value needs 'and' or 'or' in front of it.
function endsWithValue(text) {
  const trimmed = text.trimEnd();
  return trimmed !== "" && !/([&|!(<>=+\-*%,:]|\b(and|or|not))$/i.test(trimmed);
}

// Selects that build a value path (buff.<name>.<property>, a resource, a named value) and insert it into a code field.
// After a value, the 'and'/'or' select joins the new value to the condition.
function insertPicker(target) {
  const v = apl.vocabulary;
  const kinds = [
    { id: "buff", label: "Buff", names: v.auras, properties: v.scopes.buff },
    { id: "debuff", label: "Debuff on target", names: v.auras, properties: v.scopes.debuff },
    { id: "cooldown", label: "Cooldown", names: v.abilities, properties: v.scopes.cooldown },
    { id: "resource", label: "Resource", names: v.resources },
    { id: "named", label: "Fight value", names: v.named.map((n) => ({ slug: n, name: n })) },
  ];
  const picker = document.createElement("div");
  picker.className = "apl-insert";
  picker.innerHTML = `<select aria-label="Join"><option>and</option><option>or</option></select>
    <select aria-label="Value kind">${kinds.map((k) => `<option value="${k.id}">${escapeHtml(k.label)}</option>`).join("")}</select>
    <select aria-label="Name"></select><select aria-label="Property"></select>
    <button type="button" class="secondary">Insert</button>`;
  const [joinSelect, kindSelect, nameSelect, propertySelect] = picker.querySelectorAll("select");
  const showJoin = () => {
    joinSelect.hidden = !endsWithValue(target.textBeforeCursor());
  };
  for (const event of ["input", "click", "keyup", "focus"]) target.input.addEventListener(event, showJoin);
  const fill = () => {
    const kind = kinds.find((k) => k.id === kindSelect.value);
    nameSelect.innerHTML = kind.names.map((n) => `<option value="${escapeHtml(n.slug)}">${escapeHtml(n.name === n.slug ? n.slug : `${n.name} (${n.slug})`)}</option>`).join("");
    propertySelect.hidden = !kind.properties;
    propertySelect.innerHTML = (kind.properties ?? []).map((p) => `<option>${escapeHtml(p)}</option>`).join("");
  };
  kindSelect.addEventListener("change", fill);
  picker.querySelector("button").addEventListener("click", () => {
    const kind = kinds.find((k) => k.id === kindSelect.value);
    if (!nameSelect.value) return;
    const value = kind.properties ? `${kind.id}.${nameSelect.value}.${propertySelect.value}` : nameSelect.value;
    const before = target.textBeforeCursor();
    const join = endsWithValue(before) ? `${/\s$/.test(before) ? "" : " "}${joinSelect.value} ` : "";
    target.insert(join + value);
  });
  fill();
  showJoin();
  return picker;
}

// ---- Actions -----------------------------------------------------------------------------

function abilityName(slug) {
  return apl.vocabulary?.abilities.find((a) => a.slug === slug)?.name ?? slug;
}

function selectionName(selection) {
  return apl.vocabulary?.targetSelections.find((s) => s.selection === selection);
}

// run_action_list and call_action_list run another list.
function isListAction(action) {
  return apl.vocabulary?.listActions.includes(action.ability) ?? false;
}

// "run_action_list" -> "Run action list".
function listActionName(name) {
  const words = name.replaceAll("_", " ");
  return words[0].toUpperCase() + words.slice(1);
}

function actionTitle(action) {
  if (isListAction(action)) return `${listActionName(action.ability)}: ${action.actionList || "pick a list"}`;
  return abilityName(action.ability) || "Pick an ability";
}

function isDefaultList(list) {
  return list.name === apl.vocabulary.defaultList;
}

function listTitle(list) {
  return isDefaultList(list) ? "Default list" : `Action list ${list.name}`;
}

function isRunAction(action) {
  return action.ability === apl.vocabulary.listActions[0];
}

// True when the list casts on every decision: it has a repeatable ability with no condition or target selection, or a
// run_action_list with no condition naming a list that casts on every decision.
function alwaysCasts(name, seen = new Set()) {
  const list = apl.lists.find((l) => l.name === name);
  if (!list || seen.has(name)) return false;
  seen.add(name);
  return list.actions.some((action) => {
    if (action.condition?.trim()) return false;
    if (isRunAction(action)) return alwaysCasts(action.actionList, seen);
    return apl.vocabulary.repeatableAbilities.includes(action.ability) && !action.targetSelection;
  });
}

// The warning for a list a run_action_list names, when the list can cast nothing.
function runWarning(name) {
  if (!name || alwaysCasts(name)) return null;
  const example = apl.vocabulary.repeatableAbilities[0];
  return `When nothing in ${name} is ready, the hero casts nothing and the actions after this one do not run. ` +
    `End ${name} with an action that is always ready${example ? `, such as ${abilityName(example)} with no condition` : ""}.`;
}

// Lists named by a run_action_list that can cast nothing.
function listWarning(list) {
  const runBy = apl.lists.some((l) => l.actions.some((a) => isRunAction(a) && a.actionList === list.name));
  if (!runBy || alwaysCasts(list.name)) return null;
  const example = apl.vocabulary.repeatableAbilities[0];
  return `A Run action list action names this list. When nothing here is ready, the hero casts nothing. ` +
    `End it with an action that is always ready${example ? `, such as ${abilityName(example)} with no condition` : ""}.`;
}

// Warnings depend on other lists' actions, so each change redraws them.
function refreshWarnings() {
  for (const list of apl.lists) {
    for (const action of list.actions.filter(isRunAction)) {
      const element = document.querySelector(`.apl-action[data-key="${action.key}"] .apl-summary`);
      if (element) renderSummary(action, element);
    }
    const section = [...document.querySelectorAll(".apl-list")].find((s) => s.list === list);
    if (section) renderListWarning(list, section.querySelector(".apl-list-warning"));
  }
}

function renderListWarning(list, element) {
  const warning = listWarning(list);
  element.hidden = !warning;
  element.textContent = warning ?? "";
}

async function renderSummary(action, element) {
  const clauses = [];
  const warning = isRunAction(action) ? runWarning(action.actionList) : null;
  if (action.targetExpression?.trim()) {
    const check = await checkExpression(action.targetExpression).catch(() => null);
    const label = selectionName(action.targetSelection ?? "First")?.label ?? "";
    clauses.push(`<div class="apl-clause"><span class="apl-keyword">target</span> ${escapeHtml(label.toLowerCase())} ${highlight(action.targetExpression, check)}</div>`);
  }
  if (action.condition?.trim()) {
    const check = await checkExpression(action.condition).catch(() => null);
    clauses.push(`<div class="apl-clause"><span class="apl-keyword">if</span> ${highlight(action.condition, check)}</div>`);
  }
  if (warning) clauses.push(`<p class="apl-field-warning">${escapeHtml(warning)}</p>`);
  element.innerHTML = clauses.join("");
}

function renderActions() {
  const container = $("apl-lists");
  container.innerHTML = "";
  for (const list of apl.lists) container.append(listSection(list));
}

// A list name is one identifier: letters, digits and '_', and none of the words 'and', 'or' and 'not'.
function listNameProblem(name, list = null) {
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) return "Name the list with letters, digits and '_', starting with a letter.";
  if (/^(and|or|not)$/i.test(name)) return `\`${name}\` is an operator.`;
  if (apl.lists.some((l) => l !== list && l.name === name)) return `There is already a list named ${name}.`;
  return null;
}

function listSection(list) {
  const section = document.createElement("section");
  section.className = "apl-list";
  section.innerHTML = isDefaultList(list)
    ? `<h3>Default list</h3>`
    : `<div class="apl-list-head">
        <h3><label for="apl-list-${escapeHtml(list.name)}">Action list</label></h3>
        <input id="apl-list-${escapeHtml(list.name)}" class="apl-list-name" spellcheck="false" autocomplete="off">
        <button type="button" class="icon" data-remove-list title="Remove list">&#x2715;</button>
      </div>`;
  section.list = list;
  const warning = document.createElement("p");
  warning.className = "apl-field-warning apl-list-warning";
  renderListWarning(list, warning);
  const actions = document.createElement("ol");
  actions.className = "apl-actions";
  list.actions.forEach((action, index) => actions.append(actionItem(list, action, index)));
  const add = document.createElement("button");
  add.type = "button";
  add.className = "secondary";
  add.textContent = "Add action";
  add.addEventListener("click", () => {
    const action = newAction();
    list.actions.push(action);
    apl.open.add(action.key);
    changed(true);
  });
  section.append(warning, actions, add);

  const nameInput = section.querySelector(".apl-list-name");
  if (nameInput) {
    nameInput.value = list.name;
    nameInput.addEventListener("change", () => {
      const name = nameInput.value.trim();
      const problem = listNameProblem(name, list);
      if (problem) {
        setMessage("apl-message", problem, "error");
        nameInput.value = list.name;
        return;
      }
      setMessage("apl-message", "");
      for (const action of apl.lists.flatMap((l) => l.actions)) {
        if (isListAction(action) && action.actionList === list.name) action.actionList = name;
      }
      list.name = name;
      changed(true);
    });
    section.querySelector("[data-remove-list]").addEventListener("click", () => {
      const runBy = apl.lists.find((l) => l.actions.some((a) => isListAction(a) && a.actionList === list.name));
      if (runBy) {
        setMessage("apl-message", `${listTitle(runBy)} has an action naming ${list.name}. Change that action first.`, "error");
        return;
      }
      setMessage("apl-message", "");
      apl.lists.splice(apl.lists.indexOf(list), 1);
      changed(true);
    });
  }
  return section;
}

function actionItem(list, action, index) {
  const open = apl.open.has(action.key);
  const item = document.createElement("li");
  item.className = `apl-action${open ? " open" : ""}`;
  item.dataset.key = action.key;
  item.innerHTML = `<div class="apl-head">
      <span class="apl-grip" title="Drag to reorder" draggable="true">&#x2630;</span>
      <button type="button" class="apl-toggle" aria-expanded="${open}">
        <span class="apl-number">${index + 1}</span>
        <span class="apl-ability">${escapeHtml(actionTitle(action))}</span>
      </button>
      <button type="button" class="icon" data-move="-1" title="Move up" ${index === 0 ? "disabled" : ""}>&#x2191;</button>
      <button type="button" class="icon" data-move="1" title="Move down" ${index === list.actions.length - 1 ? "disabled" : ""}>&#x2193;</button>
      <button type="button" class="icon" data-remove title="Remove">&#x2715;</button>
    </div>
    <div class="apl-summary"></div>`;
  renderSummary(action, item.querySelector(".apl-summary"));

  item.querySelector(".apl-toggle").addEventListener("click", () => {
    if (apl.open.has(action.key)) apl.open.delete(action.key);
    else apl.open.add(action.key);
    renderActions();
  });
  item.querySelectorAll("[data-move]").forEach((button) =>
    button.addEventListener("click", () => moveAction(list, index, index + Number(button.dataset.move))));
  item.querySelector("[data-remove]").addEventListener("click", () => {
    list.actions.splice(index, 1);
    apl.open.delete(action.key);
    changed(true);
  });
  dragAndDrop(item, list, index);

  if (open) item.append(actionDetail(list, action, item));
  return item;
}

function actionDetail(list, action, item) {
  const v = apl.vocabulary;
  const detail = document.createElement("div");
  detail.className = "apl-detail";
  const id = `apl-${action.key}`;
  const listAction = isListAction(action);
  const typeOptions =
    `<option value="">Cast an ability</option>` +
    v.listActions.map((name) => `<option value="${escapeHtml(name)}" ${name === action.ability ? "selected" : ""}>${escapeHtml(listActionName(name))}</option>`).join("");
  const knownAbility = v.abilities.some((a) => a.slug === action.ability);
  const abilityOptions =
    (action.ability ? "" : `<option value="" selected>Pick an ability</option>`) +
    (action.ability && !knownAbility ? `<option value="${escapeHtml(action.ability)}" selected>${escapeHtml(action.ability)} (not a ${escapeHtml(v.hero)} ability)</option>` : "") +
    v.abilities.map((a) => `<option value="${escapeHtml(a.slug)}" ${a.slug === action.ability ? "selected" : ""}>${escapeHtml(a.name)}</option>`).join("");
  const otherLists = apl.lists.filter((l) => l !== list && !isDefaultList(l));
  const knownList = otherLists.some((l) => l.name === action.actionList);
  const listOptions =
    (knownList ? "" : `<option value="" selected>${otherLists.length ? "Pick a list" : "Add an action list first"}</option>`) +
    otherLists.map((l) => `<option value="${escapeHtml(l.name)}" ${l.name === action.actionList ? "selected" : ""}>${escapeHtml(l.name)}</option>`).join("");
  const targetOptions =
    `<option value="">Current target</option>` +
    v.targetSelections.map((s) => `<option value="${s.selection}" ${s.selection === action.targetSelection ? "selected" : ""}>${escapeHtml(s.label)}</option>`).join("");
  const listHint = action.ability === v.listActions[0]
    ? "The actions after this one do not run, even when nothing in the named list is cast."
    : "When nothing in the named list is cast, the next action in this list runs.";
  detail.innerHTML = `
    <label for="${id}-type">Type</label><select id="${id}-type">${typeOptions}</select>
    ${listAction
      ? `<label for="${id}-list">List</label><div><select id="${id}-list">${listOptions}</select><p class="hint apl-list-hint">${escapeHtml(listHint)}</p></div>`
      : `<label for="${id}-ability">Ability</label><select id="${id}-ability">${abilityOptions}</select>`}
    <label>Condition</label><div data-slot="condition"></div>
    ${listAction ? "" : `<label for="${id}-target">Target</label><select id="${id}-target">${targetOptions}</select>
    <label data-target>Target by</label><div data-target data-slot="target"></div>`}
    ${action.arguments ? `<label for="${id}-arguments">Other arguments</label><input id="${id}-arguments" class="apl-arguments" spellcheck="false" autocomplete="off">` : ""}
    <label for="${id}-comment">Comment</label><textarea id="${id}-comment" class="apl-comment" rows="2"></textarea>`;

  const refreshHead = () => {
    item.querySelector(".apl-ability").textContent = actionTitle(action);
    renderSummary(action, item.querySelector(".apl-summary"));
  };

  detail.querySelector(`#${id}-type`).addEventListener("change", (event) => {
    const type = event.target.value;
    if (type) {
      if (!isListAction(action)) action.actionList = otherLists[0]?.name ?? null;
      action.ability = type;
      action.targetSelection = null;
      action.targetExpression = null;
    } else {
      action.ability = "";
      action.actionList = null;
    }
    changed(true);
  });

  if (listAction) {
    detail.querySelector(`#${id}-list`).addEventListener("change", (event) => {
      action.actionList = event.target.value || null;
      refreshHead();
      changed();
    });
  } else {
    detail.querySelector(`#${id}-ability`).addEventListener("change", (event) => {
      action.ability = event.target.value;
      refreshHead();
      changed();
    });
  }

  const condition = codeField(action.condition, `Always. Example: ${v.resources[0]?.slug ?? "active_enemies"} >= 3 and not buff.${v.auras[0]?.slug ?? "name"}.up`, (text) => {
    action.condition = text;
    refreshHeadSoon();
    changed();
  });
  condition.append(insertPicker(condition));
  detail.querySelector('[data-slot="condition"]').append(condition);

  if (!listAction) {
    const target = codeField(action.targetExpression, `Value for each enemy. Example: debuff.${v.auras[0]?.slug ?? "name"}.remains`, (text) => {
      action.targetExpression = text;
      refreshHeadSoon();
      changed();
    });
    target.append(insertPicker(target));
    detail.querySelector('[data-slot="target"]').append(target);

    const showTarget = () => detail.querySelectorAll("[data-target]").forEach((el) => (el.hidden = !action.targetSelection));
    detail.querySelector(`#${id}-target`).addEventListener("change", (event) => {
      action.targetSelection = event.target.value || null;
      if (!action.targetSelection) action.targetExpression = null;
      showTarget();
      refreshHead();
      changed();
    });
    showTarget();
  }

  const argumentsInput = detail.querySelector(`#${id}-arguments`);
  if (argumentsInput) {
    argumentsInput.value = action.arguments;
    argumentsInput.addEventListener("input", () => {
      action.arguments = argumentsInput.value;
      changed();
    });
  }

  const comment = detail.querySelector(`#${id}-comment`);
  comment.value = action.comment ?? "";
  comment.addEventListener("input", () => {
    action.comment = comment.value;
    changed();
  });

  const refreshHeadSoon = debounce(refreshHead, 300);
  return detail;
}

function moveAction(list, from, to) {
  if (to < 0 || to >= list.actions.length || from === to) return;
  const [action] = list.actions.splice(from, 1);
  list.actions.splice(to, 0, action);
  changed(true);
}

let dragFrom = null; // { list, index }

// An action moves within its own list.
function dragAndDrop(item, list, index) {
  const grip = item.querySelector(".apl-grip");
  grip.addEventListener("dragstart", (event) => {
    dragFrom = { list, index };
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setDragImage(item, 20, 20);
    item.classList.add("dragging");
  });
  grip.addEventListener("dragend", () => {
    dragFrom = null;
    item.classList.remove("dragging");
  });
  // Dropping on an action puts the dragged one in its place: above it when dragged up, below it when dragged down.
  const dropClass = () => (dragFrom.index < index ? "drop-after" : "drop-before");
  item.addEventListener("dragover", (event) => {
    if (dragFrom?.list !== list || dragFrom.index === index) return;
    event.preventDefault();
    item.classList.add(dropClass());
  });
  item.addEventListener("dragleave", () => item.classList.remove("drop-before", "drop-after"));
  item.addEventListener("drop", (event) => {
    event.preventDefault();
    item.classList.remove("drop-before", "drop-after");
    // The move re-renders the list, so the dragged item's dragend never fires.
    const from = dragFrom;
    dragFrom = null;
    if (from?.list === list) moveAction(list, from.index, index);
  });
}

$("apl-add-list").addEventListener("click", () => {
  const name = $("apl-new-list").value.trim();
  const problem = listNameProblem(name);
  if (problem) {
    setMessage("apl-message", problem, "error");
    $("apl-new-list").focus();
    return;
  }
  setMessage("apl-message", "");
  const action = newAction();
  apl.lists.push({ name, actions: [action] });
  apl.open.add(action.key);
  $("apl-new-list").value = "";
  changed(true);
});

// ---- Text --------------------------------------------------------------------------------

// Every action in list order, with its list's name.
function documentActions() {
  return apl.lists.flatMap((list) => list.actions.map(({ key, ...fields }) => ({ ...fields, list: list.name })));
}

function errorText(errors) {
  const labels = apl.lists.flatMap((list) => list.actions.map((_, i) => `${listTitle(list)} action ${i + 1}`));
  return errors.map((e) => `${labels[e.action]}: ${e.message}`).join(" ");
}

const writeText = debounce(async () => {
  try {
    const result = await api("/api/apl/write", { method: "POST", body: JSON.stringify({ actions: documentActions() }) });
    if (result.text != null) {
      $("apl-text").value = result.text;
      setMessage("apl-text-message", "");
    } else {
      setMessage("apl-text-message", errorText(result.errors), "error");
    }
  } catch (error) {
    setMessage("apl-text-message", error.message, "error");
  }
}, 250);

function changed(structural = false) {
  if (structural) renderActions();
  else refreshWarnings();
  writeText();
}

// The default list first, then the other lists in order of their first action.
function loadActions(actions) {
  apl.lists = [{ name: apl.vocabulary.defaultList, actions: [] }];
  for (const fields of actions) {
    let list = apl.lists.find((l) => l.name === fields.list);
    if (!list) apl.lists.push((list = { name: fields.list, actions: [] }));
    list.actions.push(newAction(fields));
  }
  apl.open.clear();
  changed(true);
}

$("apl-load-text").addEventListener("click", async () => {
  try {
    const result = await api("/api/apl/parse", { method: "POST", body: JSON.stringify({ text: $("apl-text").value }) });
    loadActions(result.actions);
    setMessage("apl-message", `Loaded ${result.actions.length} actions.`, "ok");
  } catch (error) {
    setMessage("apl-text-message", error.message, "error");
  }
});

// ---- Heroes and saved lists --------------------------------------------------------------

async function selectHero(hero) {
  apl.hero = hero;
  $("apl-hero").value = hero;
  apl.vocabulary = await api(`/api/apl/heroes/${encodeURIComponent(hero)}/vocabulary`);
  renderActions();
}

async function loadSavedLists(selected = "") {
  const lists = await api("/api/apl/lists");
  $("apl-saved").innerHTML =
    `<option value="">New action list</option>` +
    lists.map((l) => {
      const value = `${l.hero}/${l.name}`;
      return `<option value="${escapeHtml(value)}" ${value === selected ? "selected" : ""}>${escapeHtml(l.name)} (${escapeHtml(l.hero)})</option>`;
    }).join("");
}

$("apl-hero").addEventListener("change", () => selectHero($("apl-hero").value).catch((error) => setMessage("apl-message", error.message, "error")));

$("apl-saved").addEventListener("change", async () => {
  const value = $("apl-saved").value;
  setMessage("apl-message", "");
  if (!value) {
    $("apl-name").value = "";
    loadActions([]);
    return;
  }
  const [hero, name] = value.split("/");
  try {
    const saved = await api(`/api/apl/lists/${encodeURIComponent(hero)}/${encodeURIComponent(name)}`);
    await selectHero(hero);
    $("apl-name").value = name;
    if (saved.error) {
      $("apl-text").value = saved.text;
      $("apl-text").closest("details").open = true;
      setMessage("apl-message", `The saved text does not parse: ${saved.error}`, "error");
      return;
    }
    loadActions(saved.actions);
  } catch (error) {
    setMessage("apl-message", error.message, "error");
  }
});

$("apl-save").addEventListener("click", async () => {
  const name = $("apl-name").value.trim();
  if (!name) {
    setMessage("apl-message", "Name the action list first.", "error");
    $("apl-name").focus();
    return;
  }
  try {
    const response = await fetch(`/api/apl/lists/${encodeURIComponent(apl.hero)}/${encodeURIComponent(name)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ actions: documentActions() }),
    });
    const result = await response.json();
    if (!response.ok) {
      const details = errorText(result.errors ?? []);
      setMessage("apl-message", `${result.error ?? "Saving failed."} ${details}`.trim(), "error");
      return;
    }
    setMessage("apl-message", `Saved to ${result.path}.`, "ok");
    await loadSavedLists(`${apl.hero}/${name}`);
  } catch (error) {
    setMessage("apl-message", error.message, "error");
  }
});

(async () => {
  try {
    const heroes = await api("/api/apl/heroes");
    $("apl-hero").innerHTML = heroes.map((h) => `<option>${escapeHtml(h)}</option>`).join("");
    await Promise.all([selectHero(heroes[0]), loadSavedLists()]);
    loadActions([]);
  } catch (error) {
    setMessage("apl-message", error.message, "error");
  }
})();
