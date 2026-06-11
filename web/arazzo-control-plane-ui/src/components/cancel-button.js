// <arazzo-cancel-button> — one-click cancel of a non-terminal run, with an optional reason + confirm.
//
//   <arazzo-cancel-button base-url="/arazzo/v1" runid="abc"></arazzo-cancel-button>
//
// Attributes : base-url, runid, label (default "Cancel"), disabled, no-confirm
// Properties : .client
// Events     : run-cancelled {run}, error {problem}
//
// The lowest-risk mutation, deliberately its own tiny element so a host can embed *just* cancel.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoCancelButton extends ArazzoElement {
  static get observedAttributes() {
    return ['label', 'disabled'];
  }

  connectedCallback() {
    this.render();
  }

  attributeChangedCallback() {
    if (this.isConnected) this.render();
  }

  get runId() { return this.getAttribute('runid'); }

  render() {
    const label = this.getAttribute('label') || 'Cancel';
    const disabled = this.hasAttribute('disabled');
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog {
          border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg);
          color: var(--_text); padding: 0; width: min(420px, 92vw);
        }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        .content { padding: 16px; display: grid; gap: 10px; }
        label { font-size: 12px; color: var(--_muted); }
        textarea { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); resize: vertical; }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <button class="trigger" type="button" ${disabled ? 'disabled' : ''}>${escapeHtml(label)}</button>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">Cancel run</div>
          <div class="content">
            <div>Mark this run <strong>Cancelled</strong>? Only non-terminal runs can be cancelled.</div>
            <label for="reason">Reason (optional)</label>
            <textarea id="reason" rows="2" placeholder="e.g. superseded by a newer run"></textarea>
            <div class="error-banner" hidden></div>
          </div>
          <div class="foot">
            <button value="dismiss" class="ghost" type="submit">Keep running</button>
            <button value="confirm" class="danger confirm" type="submit">Cancel run</button>
          </div>
        </form>
      </dialog>
    `;
    this.$('.trigger').addEventListener('click', () => this.start());
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') {
        e.preventDefault();
        this.confirm();
      }
    });
  }

  start() {
    if (this.hasAttribute('disabled')) return;
    if (this.hasAttribute('no-confirm')) { this.confirm(); return; }
    const banner = this.$('.error-banner');
    banner.hidden = true;
    this.$('#reason').value = '';
    this.$('dialog').showModal();
  }

  async confirm() {
    const client = this.client;
    const runId = this.runId;
    const banner = this.$('.error-banner');
    if (!client || !runId) return;
    const reason = this.$('#reason')?.value?.trim() || undefined;
    const confirmBtn = this.$('.confirm');
    confirmBtn.disabled = true;
    try {
      const run = await client.cancelRun(runId, { reason });
      this.$('dialog').close();
      this.emit('run-cancelled', { run });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      banner.textContent = `${problem.title || 'Cancel failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      banner.hidden = false;
      this.emit('error', { problem, error: err });
    } finally {
      confirmBtn.disabled = false;
    }
  }
}

define('arazzo-cancel-button', ArazzoCancelButton);
export { ArazzoCancelButton };
