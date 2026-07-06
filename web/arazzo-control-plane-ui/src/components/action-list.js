// buildActionList — the shared "list of success/failure actions" section used by the step
// inspector (local onSuccess/onFailure) and the workflow inspector (the inherited-defaults layer).
// Each action is a collapsible <details> row hosting an <arazzo-action-editor>; add/remove/reorder
// mutate the list and call back. A DOM-building helper, not a component: both inspectors own the
// surrounding form and just mount these sections.
//
// ORDER IS SEMANTICS: Arazzo dispatch is first-match-wins in declaration order, and an action
// with no criteria (a catch-all) always matches. The list therefore makes order first-class:
// ▲▼ reorder criteria'd actions; catch-alls are PINNED to the end (no reorder controls, criteria'd
// actions cannot move below them, additions insert above them); an action that loses its last
// criterion becomes a catch-all and moves to the end; anything that still ends up after a
// catch-all (e.g. a hand-authored document) is flagged unreachable rather than silently rewritten.

import './action-editor.js';

/** An action with no criteria always matches — the list's catch-all. */
const isCatchAll = (a) => a && typeof a.reference !== 'string' && !(a.criteria?.length);

/**
 * @param {object} opts
 * @param {object[]} opts.actions        The live action list (mutated in place).
 * @param {'success'|'failure'} opts.kind
 * @param {string[]} opts.stepIds        Step targets offered by the embedded editors.
 * @param {string[]} [opts.workflowIds]  Cross-workflow targets offered by the embedded editors.
 * @param {object}   [opts.components]    The document's components map for this kind (name → action) —
 *                                        enables the add-reference affordance and localize.
 * @param {object} opts.completionContext
 * @param {() => void} opts.onChange     Called after any mutation (edit, add, remove, reorder).
 * @param {(name: string, action: object) => void} [opts.onComponentChange]
 *                                        Called when a SHARED (components) action is edited in
 *                                        place or a local action is promoted — the host persists
 *                                        the components map. Without it, shared editing is off.
 * @returns {HTMLElement}
 */
export function buildActionList({ actions, kind, stepIds, workflowIds, completionContext, components, onChange, onComponentChange }) {
  const root = document.createElement('div');
  root.className = `action-list ${kind}`;

  const summaryText = (action) =>
    `${action.name || '(unnamed)'} · ${action.type}${action.stepId ? ` → ${action.stepId}` : ''}${isCatchAll(action) ? ' · catch-all' : ''}`;

  const firstCatchAllIndex = () => {
    const i = actions.findIndex(isCatchAll);
    return i < 0 ? actions.length : i;
  };

  const render = () => {
    root.replaceChildren();
    let sawCatchAll = false;
    actions.forEach((action, i) => {
      if (action && typeof action.reference === 'string') {
        const key = action.reference.split('.').pop();
        const resolved = components?.[key];

        // A SHARED action is properly editable in place: expanding edits every reference in the
        // document; "this instance only" copies it inline so it can diverge here.
        const details = document.createElement('details');
        details.className = 'reusable-row-details';
        const summary = document.createElement('summary');
        const title = document.createElement('span');
        title.className = 'atitle';
        title.textContent = `↺ ${action.reference} · shared${resolved ? '' : ' (unresolved)'}`;
        summary.append(title);

        if (resolved) {
          const localize = document.createElement('button');
          localize.type = 'button';
          localize.className = 'ghost move';
          localize.textContent = 'this instance only';
          localize.title = 'Copy the shared action inline so THIS list can edit it independently — other references keep the shared one';
          localize.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            actions[i] = structuredClone(resolved);
            render();
            onChange();
          });
          summary.append(localize);
        }

        const drop = document.createElement('button');
        drop.type = 'button';
        drop.className = 'ghost move';
        drop.textContent = '✕';
        drop.title = 'Remove the reference (the shared action stays in the library)';
        drop.addEventListener('click', (e) => {
          e.preventDefault();
          e.stopPropagation();
          actions.splice(i, 1);
          render();
          onChange();
        });
        summary.append(drop);
        details.append(summary);

        if (resolved && onComponentChange) {
          const hint = document.createElement('div');
          hint.className = 'shared-hint';
          hint.textContent = 'Editing the SHARED action — every reference in the document follows.';
          const editor = document.createElement('arazzo-action-editor');
          editor.kind = kind;
          editor.stepIds = stepIds;
          editor.workflowIds = workflowIds || [];
          editor.completionContext = completionContext;
          editor.value = resolved;
          editor.addEventListener('action-changed', (e) => {
            e.stopPropagation();
            components[key] = e.detail.action;
            title.textContent = `↺ ${action.reference} · shared`;
            onComponentChange(key, e.detail.action);
          });
          details.append(hint, editor);
        }

        root.append(details);
        return;
      }
      const unreachable = sawCatchAll;
      if (isCatchAll(action)) sawCatchAll = true;

      const details = document.createElement('details');
      const summary = document.createElement('summary');
      const title = document.createElement('span');
      title.className = 'atitle';
      title.textContent = summaryText(action);
      summary.append(title);

      // Reorder: criteria'd actions move among themselves; catch-alls stay pinned at the end.
      if (!isCatchAll(action)) {
        const pin = firstCatchAllIndex();
        const controls = document.createElement('span');
        controls.className = 'reorder';
        for (const [glyph, delta, disabled] of [['▲', -1, i === 0], ['▼', +1, i + 1 >= Math.min(pin, actions.length)]]) {
          const btn = document.createElement('button');
          btn.type = 'button';
          btn.className = 'ghost move';
          btn.textContent = glyph;
          btn.title = delta < 0 ? 'Match earlier' : 'Match later';
          btn.disabled = disabled;
          btn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation(); // don't toggle the <details>
            const j = i + delta;
            [actions[i], actions[j]] = [actions[j], actions[i]];
            render();
            onChange();
          });
          controls.append(btn);
        }
        summary.append(controls);
      }
      if (unreachable) {
        const warn = document.createElement('span');
        warn.className = 'dead';
        warn.title = 'A catch-all (no criteria) appears earlier in the list and always matches first.';
        warn.textContent = '⚠ unreachable';
        summary.append(warn);
      }
      details.append(summary);

      const editor = document.createElement('arazzo-action-editor');
      editor.kind = kind;
      editor.stepIds = stepIds;
      editor.workflowIds = workflowIds || [];
      editor.completionContext = completionContext;
      editor.value = action;
      editor.addEventListener('action-changed', (e) => {
        e.stopPropagation();
        const wasCatchAll = isCatchAll(actions[i]);
        actions[i] = e.detail.action;
        title.textContent = summaryText(e.detail.action);
        // Crossing the catch-all boundary repositions the action: a new catch-all pins to the
        // end; one that gained criteria rejoins the ordered section.
        if (isCatchAll(e.detail.action) !== wasCatchAll) {
          const [moved] = actions.splice(i, 1);
          actions.splice(isCatchAll(moved) ? actions.length : firstCatchAllIndex(), 0, moved);
          render();
        }
        onChange();
      });

      const remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'ghost remove';
      remove.textContent = '✕ Remove action';
      remove.addEventListener('click', () => {
        actions.splice(i, 1);
        render();
        onChange();
      });

      details.append(editor, remove);

      // Promote: the local action moves to the components library and this entry becomes a
      // reference — the inverse of "this instance only".
      if (onComponentChange && components) {
        const promote = document.createElement('button');
        promote.type = 'button';
        promote.className = 'ghost remove promote';
        promote.textContent = '↺ Make shared';
        promote.title = action.name
          ? 'Move this action into the components library and reference it here — other steps can then reference it too'
          : 'Name the action first — the library is keyed by name';
        promote.disabled = !action.name;
        promote.addEventListener('click', () => {
          let name = action.name;
          for (let n = 2; components[name] && JSON.stringify(components[name]) !== JSON.stringify(action); n++) name = `${action.name}-${n}`;
          const shared = structuredClone(action);
          shared.name = name;
          components[name] = shared;
          onComponentChange(name, shared);
          actions[i] = { reference: `$components.${kind === 'success' ? 'successActions' : 'failureActions'}.${name}` };
          render();
          onChange();
        });
        details.append(promote);
      }
      root.append(details);
    });

    const add = document.createElement('button');
    add.type = 'button';
    add.className = 'ghost add-action';
    add.textContent = `+ Add ${kind} action`;
    add.addEventListener('click', () => {
      // A fresh action starts criteria-less (a catch-all), so it belongs at the end anyway.
      actions.push({ name: '', type: 'end' });
      render();
      root.querySelector('details:last-of-type')?.setAttribute('open', '');
      onChange();
    });
    root.append(add);

    // Reference a reusable component action ($components.successActions/… by kind).
    const keys = Object.keys(components || {});
    if (keys.length) {
      const refSelect = document.createElement('select');
      refSelect.className = 'add-ref';
      refSelect.innerHTML = `<option value="">+ Add reference…</option>`
        + keys.map((k) => `<option value="${k}">$components.${kind === 'success' ? 'successActions' : 'failureActions'}.${k}</option>`).join('');
      refSelect.addEventListener('change', () => {
        if (!refSelect.value) return;
        actions.push({ reference: `$components.${kind === 'success' ? 'successActions' : 'failureActions'}.${refSelect.value}` });
        render();
        onChange();
      });
      root.append(refSelect);
    }
  };

  render();
  return root;
}

/** The CSS both inspectors include alongside SHARED_CSS for these sections. */
export const ACTION_LIST_CSS = `
  .action-list { display: grid; gap: 6px; min-width: 0; }
  .action-list details, .action-list details > arazzo-action-editor { min-width: 0; }
  .action-list details { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); padding: 0; }
  .action-list summary { cursor: pointer; padding: 7px 10px; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; display: flex; align-items: center; gap: 6px; }
  .action-list summary .atitle { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .action-list.success summary::before { content: '✓'; color: var(--arazzo-status-completed, #2a8a4a); }
  .action-list.failure summary::before { content: '✗'; color: var(--arazzo-status-faulted, #d4351c); }
  .action-list .reorder { display: inline-flex; gap: 2px; }
  .action-list .move { font-size: 10px; padding: 1px 6px; }
  .action-list .dead { font-size: 11px; color: var(--arazzo-status-suspended, #b07d18); flex: none; }
  .action-list details[open] { padding-bottom: 10px; }
  .action-list details > arazzo-action-editor { display: block; padding: 4px 10px 6px; }
  .action-list .remove { margin: 0 10px; font-size: 11px; }
  .action-list .add-action { font-size: 12px; justify-self: start; }
  .action-list .reusable-row { font-size: 12px; padding: 6px 2px; display: flex; align-items: center; gap: 6px; }
  .action-list .reusable-row span { flex: 1; min-width: 0; }
  .action-list .reusable-row-details summary { color: var(--_muted); }
  .action-list .shared-hint { font-size: 10.5px; color: var(--arazzo-status-suspended, #b45309); padding: 0 10px 4px; }
  .action-list .promote { margin: 6px 10px 0; font-size: 11px; }
  .action-list .add-ref { font-size: 12px; justify-self: start; max-width: 100%; min-width: 0; }
`;
