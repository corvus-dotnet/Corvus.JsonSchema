// <arazzo-access-request-dialog> — the §16.5 "request access" submit form, reusable as a standalone dialog.
//
//   const dlg = document.createElement('arazzo-access-request-dialog');
//   dlg.client = client;                       // an ArazzoControlPlaneClient
//   host.shadowRoot.appendChild(dlg);
//   dlg.open();                                // pick any workflow (autocomplete)
//   dlg.open({ baseWorkflowId: 'onboard-customer', lockWorkflow: true });  // fixed to one workflow
//
// Properties : .client
// Methods    : open({ baseWorkflowId?, lockWorkflow? }), close()
// Events     : access-request-submitted {request}, error {problem}
//
// Shared by <arazzo-access-requests> (the My-requests "Request access…" button, free workflow choice) and by the
// workflow catalog entry (<arazzo-catalog-detail>, locked to that workflow). An approval is capped to run access
// (§16.5.2), so the form offers exactly the run verbs.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './workflow-id-input.js';

const REQUESTABLE_SCOPES = [
  { scope: 'runs:write', label: 'Trigger / resume / cancel runs (runs:write)' },
  { scope: 'runs:read', label: 'Read runs (runs:read)' },
];

class ArazzoAccessRequestDialog extends ArazzoElement {
  /** The Layer-0 client used to submit. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** Open the form. With `{ baseWorkflowId, lockWorkflow: true }` the workflow is fixed (a catalog entry). */
  open({ baseWorkflowId = '', lockWorkflow = false } = {}) {
    this._baseWorkflowId = baseWorkflowId || '';
    this._lock = !!(lockWorkflow && baseWorkflowId);
    this.render();
    this.$('dialog').showModal();
    if (this._lock) this.$('.reason-in')?.focus();
    else this.$('.sub-wf')?.shadowRoot?.querySelector('input')?.focus();
  }

  close() {
    const dlg = this.$('dialog');
    if (dlg) { dlg.close(); }
  }

  showError(message) {
    const banner = this.$('.error-banner');
    if (banner) { banner.textContent = message; banner.hidden = false; }
  }

  async submit() {
    const baseWorkflowId = this._lock ? this._baseWorkflowId : (this.$('.sub-wf')?.value.trim() || '');
    const requestedScopes = this.$$('.scope-cb').filter((c) => c.checked).map((c) => c.value);
    const reason = this.$('.reason-in').value.trim() || undefined;
    const hours = Number(this.$('.dur-in').value);
    const requestedDurationSeconds = hours > 0 ? Math.round(hours * 3600) : undefined;
    if (!baseWorkflowId || requestedScopes.length === 0) {
      this.showError('A workflow and at least one scope are required.');
      return;
    }
    try {
      const request = await this.client.submitAccessRequest({ baseWorkflowId, requestedScopes, reason, requestedDurationSeconds });
      this.emit('access-request-submitted', { request });
      this.close();
    } catch (err) {
      const problem = err.problem || { title: err.message };
      this.showError(`${problem.title || 'Request failed'}${problem.detail ? ' — ' + problem.detail : ''}`);
      this.emit('error', { problem, error: err });
    }
  }

  render() {
    const wfRow = this._lock
      ? `<div><div class="sub" style="margin-bottom:4px">Workflow</div><div class="locked-wf">${escapeHtml(this._baseWorkflowId)}</div></div>`
      : `<label>Workflow<arazzo-workflow-id-input class="sub-wf" placeholder="Workflow id…"></arazzo-workflow-id-input></label>`;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 94vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .dhead { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        .dbody { padding: 14px 16px; display: grid; gap: 12px; }
        .dfoot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
        label { font-size: 12px; color: var(--_muted); display: grid; gap: 4px; }
        input, textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        textarea { resize: vertical; min-height: 56px; }
        .locked-wf { font-weight: 600; font-size: 14px; }
        .checks { display: grid; gap: 6px; }
        .checks label { display: flex; gap: 8px; align-items: center; flex-direction: row; color: var(--_text); }
        .dur { display: flex; gap: 8px; align-items: end; }
        .dur input { width: 90px; }
        .error-banner { margin: 0 16px 12px; }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="dhead">Request access</div>
          <div class="dbody">
            ${wfRow}
            <div>
              <div class="sub" style="margin-bottom:6px">Scopes (capped to run access on approval)</div>
              <div class="checks">
                ${REQUESTABLE_SCOPES.map((s, i) => `<label><input type="checkbox" class="scope-cb" value="${s.scope}"${i === 0 ? ' checked' : ''}> ${escapeHtml(s.label)}</label>`).join('')}
              </div>
            </div>
            <label>Reason (optional)
              <textarea class="reason-in" placeholder="Why you need this access…"></textarea>
            </label>
            <div class="dur">
              <label>Duration (hours)<input class="dur-in" type="number" min="1" step="1" placeholder="default"></label>
              <span class="sub">Absent = the deployment maximum.</span>
            </div>
          </div>
          <div class="error-banner" hidden></div>
          <div class="dfoot">
            <button class="cancel ghost" type="button">Cancel</button>
            <button class="ok primary" type="button">Submit request</button>
          </div>
        </form>
      </dialog>`;
    const wf = this.$('.sub-wf');
    if (wf) { wf.client = this._client; if (this._baseWorkflowId) wf.value = this._baseWorkflowId; }
    this.$('.cancel').addEventListener('click', () => this.close());
    this.$('dialog').addEventListener('cancel', (e) => { e.preventDefault(); this.close(); });
    this.$('.ok').addEventListener('click', () => this.submit());
  }
}

define('arazzo-access-request-dialog', ArazzoAccessRequestDialog);
export { ArazzoAccessRequestDialog };