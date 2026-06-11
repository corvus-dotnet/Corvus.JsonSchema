// <arazzo-purge-dialog> — bulk reap of completed/cancelled runs older than a cutoff (PURGE /runs).
//
//   const dlg = document.querySelector('arazzo-purge-dialog');
//   dlg.client = client;
//   dlg.open();
//
// Properties : .client
// Methods    : open(), close()
// Events     : purge-completed {purgedCount}, error {problem}
//
// Destructive — runs:purge only. Two-step: configure a cutoff, then a strong confirm.

import { ArazzoElement, SHARED_CSS, escapeHtml, confirmDialog, define } from './base.js';

const PRESETS = [
  { label: '7 days', days: 7 },
  { label: '30 days', days: 30 },
  { label: '90 days', days: 90 },
];

class ArazzoPurgeDialog extends ArazzoElement {
  connectedCallback() {
    if (!this._built) this.render();
  }

  open() {
    if (!this._built) this.render();
    this.$('.error-banner').hidden = true;
    this.$('.result').hidden = true;
    this.setOlderThanDaysAgo(30);
    this.$('#limit').value = '';
    this.$('dialog').showModal();
  }

  close() {
    this.$('dialog')?.close();
  }

  setOlderThanDaysAgo(days) {
    const d = new Date(Date.now() - days * 86400000);
    // datetime-local wants local time without the timezone/seconds.
    const local = new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    this.$('#olderThan').value = local;
  }

  render() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); padding: 0; width: min(480px, 92vw); }
        dialog::backdrop { background: rgba(0,0,0,0.4); }
        .head { padding: 14px 16px; font-weight: 700; border-bottom: 1px solid var(--_border); }
        .content { padding: 16px; display: grid; gap: 12px; }
        label { font-size: 12px; color: var(--_muted); display: block; margin-bottom: 4px; }
        input { width: 100%; font: inherit; padding: 8px; border: 1px solid var(--_border); border-radius: var(--_radius); background: var(--_bg); color: var(--_text); }
        .presets { display: flex; gap: 6px; flex-wrap: wrap; }
        .presets button { font-size: 12px; padding: 4px 10px; }
        .warn { border-radius: var(--_radius); padding: 10px 12px; background: color-mix(in srgb, var(--_danger) 8%, transparent); border: 1px solid color-mix(in srgb, var(--_danger) 40%, transparent); font-size: 13px; }
        .result { border-radius: var(--_radius); padding: 10px 12px; background: color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 10%, transparent); border: 1px solid color-mix(in srgb, var(--arazzo-status-completed, #2a8a4a) 40%, transparent); }
        .foot { display: flex; gap: 8px; justify-content: flex-end; padding: 12px 16px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <form method="dialog">
          <div class="head">Purge old runs</div>
          <div class="content">
            <div class="warn">Permanently deletes <strong>completed and cancelled</strong> runs older than the cutoff. This cannot be undone.</div>
            <div>
              <label for="olderThan">Older than</label>
              <input id="olderThan" type="datetime-local">
              <div class="presets" style="margin-top:6px">
                ${PRESETS.map((p) => `<button type="button" class="ghost preset" data-days="${p.days}">${escapeHtml(p.label)}</button>`).join('')}
              </div>
            </div>
            <div><label for="limit">Limit (optional — max runs to reap this call)</label><input id="limit" type="number" min="1" step="1" placeholder="unlimited"></div>
            <div class="error-banner" hidden></div>
            <div class="result" hidden></div>
          </div>
          <div class="foot">
            <button value="dismiss" class="ghost" type="submit">Close</button>
            <button value="confirm" class="danger confirm" type="submit">Purge</button>
          </div>
        </form>
      </dialog>
    `;
    this.$$('.preset').forEach((b) => b.addEventListener('click', () => this.setOlderThanDaysAgo(Number(b.dataset.days))));
    this.$('form').addEventListener('submit', (e) => {
      if (e.submitter?.value === 'confirm') {
        e.preventDefault();
        this.submit();
      }
    });
  }

  async submit() {
    const banner = this.$('.error-banner');
    const result = this.$('.result');
    banner.hidden = true;
    result.hidden = true;

    const local = this.$('#olderThan').value;
    if (!local) { banner.textContent = 'Choose a cutoff date/time.'; banner.hidden = false; return; }
    const olderThan = new Date(local).toISOString();
    const limitRaw = this.$('#limit').value.trim();
    const request = { olderThan };
    if (limitRaw !== '') {
      const n = Number(limitRaw);
      if (!Number.isInteger(n) || n < 1) { banner.textContent = 'Limit must be a positive integer.'; banner.hidden = false; return; }
      request.limit = n;
    }

    const confirmed = await confirmDialog(this, {
      title: 'Purge runs',
      message: `Permanently purge completed/cancelled runs older than ${new Date(olderThan).toLocaleString()}? This cannot be undone.`,
      confirmLabel: 'Purge',
      danger: true,
    });
    if (!confirmed) return;

    const confirmBtn = this.$('.confirm');
    confirmBtn.disabled = true;
    try {
      const { purgedCount } = await this.client.purgeRuns(request);
      result.textContent = `Purged ${purgedCount} run${purgedCount === 1 ? '' : 's'}.`;
      result.hidden = false;
      this.emit('purge-completed', { purgedCount });
    } catch (err) {
      const problem = err.problem || { title: err.message };
      banner.textContent = `${problem.title || 'Purge failed'}${problem.detail ? ' — ' + problem.detail : ''}`;
      banner.hidden = false;
      this.emit('error', { problem, error: err });
    } finally {
      confirmBtn.disabled = false;
    }
  }
}

define('arazzo-purge-dialog', ArazzoPurgeDialog);
export { ArazzoPurgeDialog };
