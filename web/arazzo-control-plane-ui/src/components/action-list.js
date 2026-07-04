// buildActionList — the shared "list of success/failure actions" section used by the step
// inspector (local onSuccess/onFailure) and the workflow inspector (the inherited-defaults layer).
// Each action is a collapsible <details> row hosting an <arazzo-action-editor>; add/remove mutate
// the list and call back. A DOM-building helper, not a component: both inspectors own the
// surrounding form and just mount these sections.

import './action-editor.js';

/**
 * @param {object} opts
 * @param {object[]} opts.actions        The live action list (mutated in place).
 * @param {'success'|'failure'} opts.kind
 * @param {string[]} opts.stepIds        Goto targets offered by the embedded editors.
 * @param {object} opts.completionContext
 * @param {() => void} opts.onChange     Called after any mutation (edit, add, remove).
 * @returns {HTMLElement}
 */
export function buildActionList({ actions, kind, stepIds, completionContext, onChange }) {
  const root = document.createElement('div');
  root.className = `action-list ${kind}`;

  const render = () => {
    root.replaceChildren();
    actions.forEach((action, i) => {
      if (action && typeof action.reference === 'string') {
        const row = document.createElement('div');
        row.className = 'muted reusable-row';
        row.textContent = `↺ ${action.reference} (reusable — edit in components)`;
        root.append(row);
        return;
      }
      const details = document.createElement('details');
      const summary = document.createElement('summary');
      summary.textContent = `${action.name || '(unnamed)'} · ${action.type}${action.stepId ? ` → ${action.stepId}` : ''}`;
      details.append(summary);

      const editor = document.createElement('arazzo-action-editor');
      editor.kind = kind;
      editor.stepIds = stepIds;
      editor.completionContext = completionContext;
      editor.value = action;
      editor.addEventListener('action-changed', (e) => {
        e.stopPropagation();
        actions[i] = e.detail.action;
        summary.textContent = `${e.detail.action.name || '(unnamed)'} · ${e.detail.action.type}${e.detail.action.stepId ? ` → ${e.detail.action.stepId}` : ''}`;
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
      actions.push({ name: '', type: 'end' });
      render();
      root.querySelector('details:last-of-type')?.setAttribute('open', '');
      onChange();
    });
    root.append(add);
  };

  render();
  return root;
}

/** The CSS both inspectors include alongside SHARED_CSS for these sections. */
export const ACTION_LIST_CSS = `
  .action-list { display: grid; gap: 6px; min-width: 0; }
  .action-list details, .action-list details > arazzo-action-editor { min-width: 0; }
  .action-list details { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_surface); padding: 0; }
  .action-list summary { cursor: pointer; padding: 7px 10px; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; }
  .action-list.success summary::before { content: '✓ '; color: var(--arazzo-status-completed, #2a8a4a); }
  .action-list.failure summary::before { content: '✗ '; color: var(--arazzo-status-faulted, #d4351c); }
  .action-list details[open] { padding-bottom: 10px; }
  .action-list details > arazzo-action-editor { display: block; padding: 4px 10px 6px; }
  .action-list .remove { margin: 0 10px; font-size: 11px; }
  .action-list .add-action { font-size: 12px; justify-self: start; }
  .action-list .reusable-row { font-size: 12px; padding: 6px 2px; }
`;
