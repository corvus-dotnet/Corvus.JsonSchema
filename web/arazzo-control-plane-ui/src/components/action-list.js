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
 * @returns {HTMLElement}
 */
export function buildActionList({ actions, kind, stepIds, workflowIds, completionContext, components, onChange }) {
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
        const row = document.createElement('div');
        row.className = 'muted reusable-row';
        const label = document.createElement('span');
        label.textContent = `↺ ${action.reference} (reusable — edit in components)`;
        row.append(label);

        // Localize: copy the referenced action inline so it can diverge here (mirrors defaults-localize).
        const key = action.reference.split('.').pop();
        const resolved = components?.[key];
        if (resolved) {
          const localize = document.createElement('button');
          localize.type = 'button';
          localize.className = 'ghost move';
          localize.textContent = 'localize';
          localize.title = 'Copy the reusable action inline so this list can edit it independently';
          localize.addEventListener('click', () => {
            actions[i] = structuredClone(resolved);
            render();
            onChange();
          });
          row.append(localize);
        }

        const drop = document.createElement('button');
        drop.type = 'button';
        drop.className = 'ghost move';
        drop.textContent = '✕';
        drop.title = 'Remove the reference';
        drop.addEventListener('click', () => {
          actions.splice(i, 1);
          render();
          onChange();
        });
        row.append(drop);
        root.append(row);
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
  .action-list .add-ref { font-size: 12px; justify-self: start; max-width: 100%; }
`;
