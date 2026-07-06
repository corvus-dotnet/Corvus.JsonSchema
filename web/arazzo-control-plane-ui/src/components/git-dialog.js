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

/** The branch select's create sentinel. */
const NEW_BRANCH = '__new__';

class ArazzoGitDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._branchSeq = 0;
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
    if (this.windowOpener) this.$('.gh-connect').windowOpener = this.windowOpener;
    this.$('dialog').showModal();
    const seq = ++this._seq;
    try {
      const workingCopy = await this._client.getWorkingCopy(workingCopyId);
      if (seq !== this._seq) return;
      this._workingCopy = workingCopy;
      const name = this.$('.wc-name');
      if (name) name.textContent = workingCopy?.name ? ` — ${workingCopy.name}` : '';
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
        .new-branch { display: flex; gap: 6px; align-items: center; }
        .new-branch input { flex: 1; min-width: 0; }
        .specs { display: grid; gap: 4px; }
        .specs-head { font-size: 11px; }
        .spec-rows { display: grid; gap: 4px; }
        .spec-row { display: grid; grid-template-columns: minmax(9ch, auto) 1fr; gap: 8px; align-items: center; }
        .spec-row .sname { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
        .spec-row .stale { color: var(--_muted); font-size: 10.5px; }
        .result { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .result[hidden], .error-banner[hidden] { display: none; }
        .result .file { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
      </style>
      <dialog part="dialog">
        <div class="head"><h2>Git<span class="wc-name muted"></span></h2><button class="x" type="button" title="Close">✕</button></div>
        <div class="body">
          <div class="error-banner" hidden></div>
          <arazzo-github-connect class="gh-connect"></arazzo-github-connect>
          <fieldset>
            <legend>Binding — branch-per-working-copy is the natural multi-author flow (§4.7)</legend>
            <div class="two">
              <label>Repository <select class="b-repo"></select></label>
              <label>Branch <select class="b-branch" disabled title="Pick a repository first"></select></label>
            </div>
            <div class="new-branch" hidden>
              <input class="nb-name" type="text" placeholder="feature/my-flow" spellcheck="false">
              <span class="muted">from</span>
              <select class="nb-base"></select>
              <button class="nb-create" type="button" title="Create the branch from the base branch's head — a ref only, no commit">Create branch</button>
            </div>
            <label>Document path <input class="b-path" type="text" placeholder="flows/my-flow.arazzo.json"></label>
            <label>Scenarios directory <span class="muted">(optional)</span> <input class="b-scenarios" type="text" placeholder="scenarios/my-flow"></label>
            <div class="specs">
              <span class="muted specs-head">Spec paths — where each attached source lives on the branch (blank = not tracked)</span>
              <div class="spec-rows"></div>
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
    this.$('.b-repo').addEventListener('change', () => { void this.loadBranches(); });
    this.$('.b-branch').addEventListener('change', () => {
      this.$('.new-branch').hidden = this.$('.b-branch').value !== NEW_BRANCH;
      if (!this.$('.new-branch').hidden && !this.$('.nb-name').value) {
        const slug = (this._workingCopy?.name ?? 'my-flow').toLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/^-|-$/g, '');
        this.$('.nb-name').value = `feature/${slug || 'my-flow'}`;
      }

      this.updateActions();
    });
    this.$('.nb-create').addEventListener('click', () => this.createBranch());
    this.$('.c-base').addEventListener('input', (e) => { e.target.dataset.touched = '1'; });
    ['.b-path'].forEach((s) => this.$(s).addEventListener('input', () => this.updateActions()));
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
      this.$('.b-path').value = binding.path ?? '';
      this.$('.b-scenarios').value = binding.scenariosDir ?? '';
    }

    this.renderSpecRows();
    this.updateActions();
    void this.loadBranches();
  }

  // One row per ATTACHED source: the names come from the working copy, only the path is typed.
  renderSpecRows() {
    const rows = this.$('.spec-rows');
    const specPaths = this._workingCopy?.gitBinding?.specPaths ?? {};
    const attached = (this._workingCopy?.sources ?? []).map((a) => a.name);
    const names = [...new Set([...attached, ...Object.keys(specPaths)])];
    if (names.length === 0) {
      rows.innerHTML = '<span class="muted" style="font-size:11px">No sources attached — nothing to path-map.</span>';
      return;
    }

    rows.innerHTML = names.map((name) => `
      <label class="spec-row">
        <span class="sname">${escapeHtml(name)}${attached.includes(name) ? '' : ' <span class="stale">(not attached)</span>'}</span>
        <input class="b-spec" data-name="${escapeHtml(name)}" type="text" placeholder="specs/${escapeHtml(name)}.json" value="${escapeHtml(specPaths[name] ?? '')}" spellcheck="false">
      </label>`).join('');
  }

  /** The chosen branch, '' while unchosen or mid-create. */
  branchValue() {
    const value = this.$('.b-branch').value;
    return value === NEW_BRANCH ? '' : value;
  }

  // The picker browses the repo's real branches (bound branch kept even when missing remotely).
  async loadBranches() {
    const sel = this.$('.b-branch');
    const repoValue = this.$('.b-repo').value;
    const connected = !!this.$('.gh-connect').session?.connected;
    if (!repoValue || !connected) {
      sel.disabled = true;
      sel.title = connected ? 'Pick a repository first' : 'Connect GitHub first';
      sel.innerHTML = '';
      this.updateActions();
      return;
    }

    const seq = ++this._branchSeq;
    const slash = repoValue.indexOf('/');
    try {
      const list = await this._client.listRepoBranches(repoValue.slice(0, slash), repoValue.slice(slash + 1));
      if (seq !== this._branchSeq) return; // a newer repo choice superseded this load
      this._branches = list;
      const bound = this._workingCopy?.gitBinding?.branch;
      const names = list.branches.map((b) => b.name);
      const picked = bound ?? list.defaultBranch ?? names[0] ?? '';
      sel.innerHTML = names.map((name) =>
        `<option value="${escapeHtml(name)}"${name === picked ? ' selected' : ''}>${escapeHtml(name)}${name === list.defaultBranch ? ' (default)' : ''}</option>`).join('')
        + (bound && !names.includes(bound) ? `<option value="${escapeHtml(bound)}" selected>${escapeHtml(bound)} (not on remote)</option>` : '')
        + `<option value="${NEW_BRANCH}">＋ New branch…</option>`;
      sel.disabled = false;
      sel.title = 'The branch this working copy binds to';
      const base = this.$('.nb-base');
      base.innerHTML = names.map((name) => `<option value="${escapeHtml(name)}"${name === list.defaultBranch ? ' selected' : ''}>${escapeHtml(name)}</option>`).join('');
      const prBase = this.$('.c-base');
      if (prBase && !prBase.dataset.touched && list.defaultBranch) prBase.value = list.defaultBranch;
    } catch (err) {
      if (seq !== this._branchSeq) return;
      sel.disabled = true;
      sel.title = 'Branches could not be loaded';
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }

    this.$('.new-branch').hidden = this.$('.b-branch').value !== NEW_BRANCH;
    this.updateActions();
  }

  // Create the branch from the chosen base — a ref only, no commit (§4.7); re-browse, select it.
  async createBranch() {
    this.clearError();
    const repoValue = this.$('.b-repo').value;
    const name = this.$('.nb-name').value.trim();
    if (!repoValue || !name) return;
    const slash = repoValue.indexOf('/');
    try {
      await this._client.createRepoBranch(repoValue.slice(0, slash), repoValue.slice(slash + 1), {
        name,
        ...(this.$('.nb-base').value ? { from: this.$('.nb-base').value } : {}),
      });
      await this.loadBranches();
      this.$('.b-branch').value = name;
      this.$('.new-branch').hidden = true;
      this.updateActions();
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  updateActions() {
    const connected = !!this.$('.gh-connect').session?.connected;
    const formed = this.$('.b-repo').value && this.branchValue() && this.$('.b-path').value.trim();
    const bound = !!this._workingCopy?.gitBinding;

    // A disabled control carries its reason — nothing greys out silently.
    const save = this.$('.save-binding');
    save.disabled = !this._workingCopy || !formed;
    save.title = save.disabled ? 'Pick a repository and fill in the branch and document path first' : 'Store this binding on the working copy';
    const pull = this.$('.pull');
    pull.disabled = !connected || !bound;
    pull.title = pull.disabled
      ? (!connected ? 'Connect GitHub first' : 'Save a binding first — Pull reads from the bound branch')
      : 'Refresh the document, bound specs, and scenarios from the branch (etag-guarded; nothing partially applies)';
    const commit = this.$('.commit');
    commit.disabled = !connected || !bound || !this.$('.c-message').value.trim();
    commit.title = commit.disabled
      ? (!connected ? 'Connect GitHub first' : !bound ? 'Save a binding first — Commit writes to the bound branch' : 'Enter a commit message')
      : 'Write the document, bound specs, and scenario files to the branch — authored as YOUR GitHub identity (§4.7)';
  }

  // The form's gitBinding value (specPaths parsed from `name = path` lines).
  readBinding() {
    const repoValue = this.$('.b-repo').value;
    const slash = repoValue.indexOf('/');
    const binding = {
      owner: repoValue.slice(0, slash),
      repo: repoValue.slice(slash + 1),
      branch: this.branchValue(),
      path: this.$('.b-path').value.trim(),
    };
    const scenariosDir = this.$('.b-scenarios').value.trim();
    if (scenariosDir) binding.scenariosDir = scenariosDir;
    const specPaths = {};
    for (const input of this.$$('.b-spec')) {
      const path = input.value.trim();
      if (path) specPaths[input.dataset.name] = path;
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