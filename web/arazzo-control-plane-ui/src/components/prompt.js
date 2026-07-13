// promptText — pops the kit's standard input dialog (<arazzo-input-dialog>) and resolves the entered
// string, or null if cancelled. System prompt()/confirm() are banned in this UI (they are unstyled and
// break theming); this is the shared "ask for a single value" helper that any component can await.
import './input-dialog.js';

/**
 * Ask the user for a single line of text via the themed modal.
 * @param {{title?: string, message?: string, label?: string, value?: string, placeholder?: string, confirmLabel?: string}} [opts]
 * @returns {Promise<string|null>} the entered value, or null on cancel/dismiss.
 */
export async function promptText({ title, message, label = 'Name', value = '', placeholder = '', confirmLabel = 'OK' } = {}) {
  const dlg = document.createElement('arazzo-input-dialog');
  document.body.appendChild(dlg);
  try {
    return await dlg.ask({ title, message, field: { label, value, placeholder }, confirmLabel });
  } finally {
    dlg.remove();
  }
}
