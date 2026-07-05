// <arazzo-git-dialog> — bind a working copy to a branch and round-trip it (workflow-designer
// design §4.7): pull refreshes the document (+ bound specs + scenarios) from the branch; commit
// writes them back — authored as the signed-in user's GitHub-held git identity — with an optional
// draft pull request FROM the bound branch.
//
//   const dlg = document.createElement('arazzo-git-dialog');
//   dlg.client = client;
//   host.appendChild(dlg);
//   dlg.open({ workingCopyId: 'wc-…' });     // flush any pending save first: the dialog reads the STORED copy
//
// Properties : .client
// Methods    : open({ workingCopyId }), close()
// Events     : binding-saved {workingCopy} · pulled {workingCopy} · committed {result} · error {problem}
//
// The host refreshes its save token from binding-saved/pulled (both bump the etag), and reloads its
// model from pulled (the document changed underneath).

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import './github-connect.js';

class ArazzoGitDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._workingCopy = null;
    /** @private */ this._seq = 0;
  }

  /** The Layer-0 client. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** Opens for a working copy — fetched fresh, so flush any pending save first. */
  async open({ workingCopyId } = {}) {
    this._workingCopy = null;
    this.render();
    this.$('.gh-connect').client = this._client;
    this.$('dialog').showModal();
    const seq = ++this._seq;
    try {
      const workingCopy = await this._client.getWorkingCopy(workingCopyId);
      if (seq !== this._seq) return;
      this._workingCopy = workingCopy;
      this.renderBinding();
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  close() {
    this.$('dialog')?.close();
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        dialog { border: 1px solid var(--_border); border-radius: 10px; background: var(--_bg); color: inherit; padding: 0; width: min(520px, 92vw); }
        dialog::backdrop { background: rgb(0 0 0 / 0.35); }
        .head { display: flex; justify-content: space-between; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; }
        .body { padding: 12px 14px; display: grid; gap: 12px; }
        fieldset { border: 1px solid var(--_border); border-radius: 8px; padding: 10px 12px; display: grid; gap: 8px; margin: 0; }
        legend { font-size: 12px; color: var(--_muted); padding: 0 4px; }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        label.check { display: flex; gap: 6px; align-items: center; cursor: pointer; }
        label.check input { width: auto; }
        input, select, textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; min-height: 44px; }
        .two { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .row-actions { display: flex; gap: 8px; align-items: center; }
        .hint { font-size: 11px; color: var(--_muted); }
        .result { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .result[hidden], .error-banner[hidden] { display: none; }
        .result .file { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <div class="head"><h2>Git</h2><button class="x" type="button" title="Close">✕</button></div>
        <div class="body">
          <div class="error-banner" hidden></div>
          <arazzo-github-connect class="gh-connect"></arazzo-github-connect>
          <fieldset>
            <legend>Binding — branch-per-working-copy is the natural multi-author flow (§4.7)</legend>
            <div class="two">
              <label>Repository <select class="b-repo"></select></label>
              <label>Branch <input class="b-branch" type="text" placeholder="feature/my-flow"></label>
            </div>
            <label>Document path <input class="b-path" type="text" placeholder="flows/my-flow.arazzo.json"></label>
            <div class="two">
              <label>Scenarios directory <span class="muted">(optional)</span> <input class="b-scenarios" type="text" placeholder="scenarios/my-flow"></label>
              <label>Spec paths <span class="muted">(name = path, one per line)</span> <textarea class="b-specs" placeholder="petstore = specs/petstore.json"></textarea></label>
            </div>
            <div class="row-actions"><button class="save-binding" type="button" disabled>Save binding</button></div>
          </fieldset>
          <fieldset>
            <legend>Round-trip</legend>
            <div class="row-actions">
              <button class="pull" type="button" disabled title="Refresh the document, bound specs, and scenarios from the branch (etag-guarded; nothing partially applies)">⤓ Pull</button>
              <span class="hint">Pull replaces the document, bound specs, and scenario set from the branch.</span>
            </div>
            <label>Commit message <input class="c-message" type="text" placeholder="what changed"></label>
            <label class="check"><input class="c-pr" type="checkbox"> Open a draft pull request onto <input class="c-base" type="text" value="main" style="width:90px"></label>
            <div class="row-actions"><button class="commit" type="button" disabled title="Write the document, bound specs, and scenario files to the branch — authored as YOUR GitHub identity (§4.7)">⤒ Commit</button></div>
            <div class="result" hidden></div>
          </fieldset>
        </div>
        <div class="foot"><button class="done" type="button">Done</button></div>
      </dialog>`;

    this.$('button.x').addEventListener('click', () => this.close());
    this.$('button.done').addEventListener('click', () => this.close());
    this.$('.gh-connect').addEventListener('github-connected', () => this.renderBinding());
    this.$('.gh-connect').addEventListener('github-disconnected', () => this.renderBinding());
    this.$('.save-binding').addEventListener('click', () => this.saveBinding());
    this.$('.pull').addEventListener('click', () => this.pull());
    this.$('.commit').addEventListener('click', () => this.commit());
    this.$('.c-message').addEventListener('input', () => this.updateActions());
    ['.b-repo', '.b-branch', '.b-path'].forEach((s) => this.$(s).addEventListener('input', () => this.updateActions()));
  }

  showError(message) {
    const banner = this.$('.error-banner');
    banner.textContent = message;
    banner.hidden = false;
  }

  clearError() { this.$('.error-banner').hidden = true; }

  // Seeds the binding form from the working copy + the connected session's repositories.
  renderBinding() {
    const binding = this._workingCopy?.gitBinding;
    const session = this.$('.gh-connect').session;
    const repos = session?.connected ? (session.installations ?? []).flatMap((i) => i.repositories ?? []) : [];
    const sel = this.$('.b-repo');
    const current = binding ? `${binding.owner}/${binding.repo}` : '';
    const options = new Map(repos.map((r) => [`${r.owner}/${r.name}`, r.fullName]));
    if (current && !options.has(current)) options.set(current, current);
    sel.innerHTML = `<option value="">Choose…</option>` + [...options].map(([value, label]) =>
      `<option value="${escapeHtml(value)}"${value === current ? ' selected' : ''}>${escapeHtml(label)}</option>`).join('');
    if (binding) {
      this.$('.b-branch').value = binding.branch ?? '';
      this.$('.b-path').value = binding.path ?? '';
      this.$('.b-scenarios').value = binding.scenariosDir ?? '';
      this.$('.b-specs').value = Object.entries(binding.specPaths ?? {}).map(([name, path]) => `${name} = ${path}`).join('\n');
    }

    this.updateActions();
  }

  updateActions() {
    const connected = !!this.$('.gh-connect').session?.connected;
    const formed = this.$('.b-repo').value && this.$('.b-branch').value.trim() && this.$('.b-path').value.trim();
    const bound = !!this._workingCopy?.gitBinding;
    this.$('.save-binding').disabled = !this._workingCopy || !formed;
    this.$('.pull').disabled = !connected || !bound;
    this.$('.commit').disabled = !connected || !bound || !this.$('.c-message').value.trim();
  }

  // The form's gitBinding value (specPaths parsed from `name = path` lines).
  readBinding() {
    const repoValue = this.$('.b-repo').value;
    const slash = repoValue.indexOf('/');
    const binding = {
      owner: repoValue.slice(0, slash),
      repo: repoValue.slice(slash + 1),
      branch: this.$('.b-branch').value.trim(),
      path: this.$('.b-path').value.trim(),
    };
    const scenariosDir = this.$('.b-scenarios').value.trim();
    if (scenariosDir) binding.scenariosDir = scenariosDir;
    const specPaths = {};
    for (const line of this.$('.b-specs').value.split('\n')) {
      const eq = line.indexOf('=');
      if (eq > 0) {
        const name = line.slice(0, eq).trim();
        const path = line.slice(eq + 1).trim();
        if (name && path) specPaths[name] = path;
      }
    }

    if (Object.keys(specPaths).length > 0) binding.specPaths = specPaths;
    return binding;
  }

  async saveBinding() {
    this.clearError();
    try {
      const saved = await this._client.saveWorkingCopy(this._workingCopy.id, {
        document: this._workingCopy.document,
        expectedEtag: this._workingCopy.etag,
        gitBinding: this.readBinding(),
      });
      this._workingCopy = saved;
      this.renderBinding();
      this.emit('binding-saved', { workingCopy: saved });
    } catch (err) {
      this.showError(err.status === 409
        ? 'The working copy changed while this dialog was open — close and reopen to rebind.'
        : err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  async pull() {
    this.clearError();
    this.$('.pull').disabled = true;
    try {
      const pulled = await this._client.pullWorkingCopy(this._workingCopy.id, { expectedEtag: this._workingCopy.etag });
      this._workingCopy = pulled;
      this.renderBinding();
      const result = this.$('.result');
      result.hidden = false;
      result.innerHTML = `<span>Pulled from <strong>${escapeHtml(`${pulled.gitBinding.owner}/${pulled.gitBinding.repo}@${pulled.gitBinding.branch}`)}</strong>.</span>`;
      this.emit('pulled', { workingCopy: pulled });
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }

    this.updateActions();
  }

  async commit() {
    this.clearError();
    this.$('.commit').disabled = true;
    const message = this.$('.c-message').value.trim();
    const pullRequest = this.$('.c-pr').checked ? { base: this.$('.c-base').value.trim() || 'main', draft: true } : undefined;
    try {
      const result = await this._client.commitWorkingCopy(this._workingCopy.id, { message, ...(pullRequest ? { pullRequest } : {}) });
      const box = this.$('.result');
      box.hidden = false;
      box.innerHTML = `
        <span>Committed as <strong>${escapeHtml(this.$('.gh-connect').session?.login ?? 'you')}</strong> — your GitHub identity, not a service account (§4.7):</span>
        ${result.files.map((f) => `<span class="file">${escapeHtml(f.path)}</span>`).join('')}
        ${result.pullRequest ? `<span>Pull request: <a href="${escapeHtml(result.pullRequest.url)}" target="_blank" rel="noopener">#${escapeHtml(String(result.pullRequest.number))}</a></span>` : ''}`;
      this.emit('committed', { result });
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }

    this.updateActions();
  }
}

define('arazzo-git-dialog', ArazzoGitDialog);
export { ArazzoGitDialog };