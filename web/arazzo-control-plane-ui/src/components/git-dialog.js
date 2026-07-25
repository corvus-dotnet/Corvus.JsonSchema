// <arazzo-git-dialog> — bind a working copy to a branch and round-trip it (workflow-designer
// design §4.7): pull refreshes the document (+ bound sources + scenarios) from the branch; commit
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
import './git-tree.js';
import './input-dialog.js';
import './filter-input.js';
import './workflow-compare.js';

/** Decodes a brokered contents read's base64 file into a parsed JSON document. */
function decodeJsonFile(node) {
  return JSON.parse(new TextDecoder().decode(Uint8Array.from(atob((node.file?.content ?? '').replace(/\s/g, '')), (c) => c.charCodeAt(0))));
}

/** The branch select's create sentinel. */
const NEW_BRANCH = '__new__';

class ArazzoGitDialog extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._branchSeq = 0;
    /** @private */ this._workingCopy = null;
    /** @private */ this._seq = 0;
    /** @private */ this._historySeq = 0;
    /** @private */ this._historyPage = 0;
    /** @private */ this._historyKey = '';
    /** @private */ this._historyCommits = [];
    /** @private */ this._historySelection = []; // shas, oldest-pick first, MAX 2 (FIFO: a third pick evicts the first)
    /** @private */ this._historyHasMore = false;
  }

  /** The Layer-0 client. */
  set client(value) { this._client = value; }
  get client() { return this._client; }

  /** Opens for a working copy — fetched fresh, so flush any pending save first. */
  async open({ workingCopyId } = {}) {
    this._workingCopy = null;
    // render() below rebuilds the shadow DOM, wiping the .commits list. The history load dedups on
    // _historyKey, so without clearing it here loadHistory would early-return on every RE-open and
    // leave the freshly-wiped list empty — "history doesn't load when you select the Git tab".
    this._historyKey = '';
    this._historyPage = 0;
    this._historyCommits = [];
    this._historySelection = [];
    this._historyHasMore = false;
    this.render();
    this.$('.gh-connect').client = this._client;
    if (this.windowOpener) this.$('.gh-connect').windowOpener = this.windowOpener;
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
    // A panel has nothing to dismiss; kept for API compatibility.
  }

  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        /* The panel lives in the designer's narrow sidebar: everything must fit its width, never clip. Selects carry
           long options (repo, branch), so without min-width:0 they set a large min-content size that grid/flex items
           refuse to shrink below — the classic horizontal-overflow trap. Force fields to fill and shrink. */
        :host { display: block; min-width: 0; }
        .panel, .body, fieldset, label, .two, .pathrow, .new-branch, .specs, .spec-row { min-width: 0; }
        input, select, textarea, arazzo-filter-input { width: 100%; min-width: 0; box-sizing: border-box; }
        .panel { background: var(--_bg); color: inherit; }
        .head { display: flex; justify-content: space-between; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--_border); }
        .head h2 { margin: 0; font-size: 14px; }
        .body { padding: 12px 14px; display: grid; gap: 12px; }
        fieldset { border: 1px solid var(--_border); border-radius: 8px; padding: 10px 12px; display: grid; gap: 8px; margin: 0; }
        legend { font-size: 12px; color: var(--_muted); padding: 0 4px; }
        .stage-hint { font-size: 12px; color: var(--_muted); border: 1px dashed var(--_border); border-radius: 8px; padding: 10px 12px; }
        .stage-hint[hidden] { display: none; }
        .paths-section { display: grid; gap: 8px; }
        .paths-section[hidden] { display: none; }
        label { display: grid; gap: 4px; font-size: 12px; color: var(--_muted); }
        label.check { display: flex; gap: 6px; align-items: center; cursor: pointer; }
        label.check input { width: auto; }
        input, select, textarea { font: inherit; font-size: 13px; padding: 6px 8px; border: 1px solid var(--_border); border-radius: 6px; background: var(--_bg); color: var(--arazzo-text, inherit); }
        textarea { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; min-height: 44px; }
        /* minmax(0, …): a combo holding a long branch name must shrink below its content, never widen the panel. */
        .two { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 8px; }
        .row-actions { display: flex; gap: 8px; align-items: center; }
        .hint { font-size: 11px; color: var(--_muted); }
        /* The name gets its own full-width row; "from <base>" and the button wrap onto the next line,
           so the name box is never squeezed and the button never has to wrap its label. */
        /* The new-branch form floats as a dropdown anchored to the Branch field (like the browse trees
           and the kit's pickers) instead of appearing inline and pushing the dialog's content around. */
        .new-branch { position: fixed; inset: auto; margin: 0; z-index: 50; padding: 10px;
                      background: var(--_bg); border: 1px solid var(--_border); border-radius: var(--_radius);
                      box-shadow: 0 8px 24px rgb(0 0 0 / 0.28);
                      display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
        .new-branch[popover]:not(:popover-open) { display: none; }
        .new-branch[hidden] { display: none; }
        .new-branch .nb-title { flex: 1 1 100%; font-weight: 600; font-size: 12px; }
        .new-branch .nb-name { flex: 1 1 100%; min-width: 0; }
        .new-branch .nb-base { flex: 1 1 12ch; min-width: 0; }
        .new-branch .nb-create { flex: 0 0 auto; white-space: nowrap; }
        .pathrow { display: flex; gap: 6px; align-items: center; }
        /* The field (or the specs hint) takes the row so all three browse buttons right-align, and the
           buttons share a min width so they read as one consistent control regardless of label. */
        .pathrow input, .pathrow .specs-head { flex: 1; min-width: 0; }
        /* Explicit colour so a browse button reads the same whether its row sits inside a <label>
           (muted text) or the specs block (default text) — the base button inherits its colour. */
        .pathrow .ghost { flex: 0 0 auto; font-size: 11px; min-width: 6.5em; text-align: center; color: var(--_text); }
        /* The repo browser floats as a dropdown in the top layer (like the kit's pickers) instead of
           expanding inline and pushing the dialog's content around. Positioned per open from its anchor. */
        .tree-slot { position: fixed; inset: auto; margin: 0; z-index: 50; padding: 6px;
                     background: var(--_bg); border: 1px solid var(--_border); border-radius: var(--_radius);
                     box-shadow: 0 8px 24px rgb(0 0 0 / 0.28); overflow: auto; }
        .tree-slot[popover]:not(:popover-open) { display: none; }
        .tree-slot[hidden] { display: none; }
        /* The tree-slot IS the bordered, scrolling dropdown; flatten the embedded git-tree (which is a
           bordered scroll box in its own right, for standalone use) so the two do not double up into a
           border-inside-a-border with a wasteful gap. The slot owns the frame, scroll, and padding. */
        .tree-slot arazzo-git-tree::part(tree) { border: none; border-radius: 0; max-height: none; overflow: visible; padding: 0; }
        .specs { display: grid; gap: 4px; }
        .specs-head { font-size: 11px; }
        .spec-rows { display: grid; gap: 4px; }
        .spec-row { display: grid; grid-template-columns: minmax(9ch, auto) minmax(0, 1fr); gap: 8px; align-items: center; }
        .spec-row .sname { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
        .spec-row .stale { color: var(--_muted); font-size: 10.5px; }
        .result, .load-result { font-size: 12px; border: 1px solid var(--_border); border-radius: 6px; padding: 8px 10px; display: grid; gap: 2px; }
        .result[hidden], .load-result[hidden], .error-banner[hidden] { display: none; }
        .result .file { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 11px; }
        .foot { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 14px; border-top: 1px solid var(--_border); }
        /* History: the kit's headered list — title + count, actions in the header, rows below. */
        .hist { border: 1px solid var(--_border); border-radius: var(--_radius); overflow: hidden; }
        .hist .head { padding: 8px 10px; background: var(--_surface); border-bottom: 1px solid var(--_border); display: flex; align-items: center; gap: 8px; }
        .hist .head .title { font-weight: 700; font-size: 12.5px; }
        .hist .head .count { color: var(--_muted); font-size: 11.5px; }
        .hist .head .grow { flex: 1; }
        .hist .head button { font-size: 11.5px; }
        .cmp-wrap { position: relative; }
        .cmp-menu { position: absolute; top: calc(100% + 4px); right: 0; z-index: 20; min-width: 208px; display: flex; flex-direction: column; padding: 4px; gap: 2px; background: var(--_bg); border: 1px solid var(--_border); border-radius: var(--_radius); box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18); }
        .cmp-menu .menu-item { text-align: left; white-space: nowrap; border: 1px solid transparent; background: transparent; padding: 6px 10px; border-radius: 6px; font: inherit; font-size: 12.5px; cursor: pointer; }
        .cmp-menu .menu-item:hover:not(:disabled) { background: var(--_surface); }
        .cmp-menu .menu-item:disabled { color: var(--_muted); cursor: default; }
        .commits { display: grid; }
        .hist-commit { display: grid; grid-template-columns: minmax(0, 1fr); gap: 2px; padding: 6px 10px; cursor: pointer;
                  border-bottom: 1px solid var(--_border); }
        .hist-commit:last-child { border-bottom: none; }
        .hist-commit:hover { background: var(--_surface); }
        .hist-commit[aria-selected="true"] { background: color-mix(in srgb, var(--_accent) 12%, transparent); box-shadow: inset 3px 0 0 var(--_accent); }
        .hist-commit .msg { font-size: 12.5px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
        .hist-commit .meta { font-size: 11px; color: var(--_muted); font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
                        overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
        .history-more { border: none; border-top: 1px solid var(--_border); border-radius: 0; width: 100%; }
        .history-empty { font-size: 11.5px; color: var(--_muted); padding: 8px 10px; }
      </style>
      <div class="panel" part="panel">
        <arazzo-input-dialog class="ask"></arazzo-input-dialog>
        <div class="body">
          <div class="error-banner" hidden></div>
          <arazzo-github-connect class="gh-connect"></arazzo-github-connect>
          <div class="stage-hint connect-hint">Connect your GitHub identity to bind this working copy to a branch.</div>
          <fieldset class="binding-section" hidden>
            <legend>Binding — the repository, branch, and document this working copy syncs with</legend>
            <div class="two">
              <label>Repository <arazzo-filter-input class="b-repo" aria-label="Repository" placeholder="type to filter, or any owner/repo"></arazzo-filter-input></label>
              <label>Branch <arazzo-filter-input class="b-branch" aria-label="Branch" placeholder="type to filter branches" title="Pick a repository first"></arazzo-filter-input></label>
            </div>
            <div class="new-branch" popover="manual" hidden>
              <div class="nb-title">New branch</div>
              <input class="nb-name" type="text" placeholder="feature/my-flow" spellcheck="false">
              <span class="muted">from</span>
              <arazzo-filter-input class="nb-base" aria-label="Base branch" placeholder="type to filter branches"></arazzo-filter-input>
              <button class="nb-create" type="button" title="Create the branch from the base branch's head — a ref only, no commit">Create branch</button>
            </div>
            <div class="stage-hint paths-hint">Pick a repository and branch — the paths below browse it.</div>
            <div class="paths-section" hidden>
            <label>Document path
              <span class="pathrow">
                <input class="b-path" type="text" placeholder="flows/my-flow.arazzo.json">
                <button class="browse-path ghost" type="button" disabled title="Connect and pick a repository and branch first">browse…</button>
              </span>
            </label>
            <div class="tree-slot tree-path" popover="manual" hidden></div>
            <label>Scenarios directory <span class="muted">(optional)</span>
              <span class="pathrow">
                <input class="b-scenarios" type="text" placeholder="scenarios/my-flow">
                <button class="browse-scenarios ghost" type="button" disabled title="Connect and pick a repository and branch first">browse…</button>
              </span>
            </label>
            <div class="tree-slot tree-scenarios" popover="manual" hidden></div>
            <div class="specs">
              <span class="pathrow">
                <span class="muted specs-head">Source paths — where each attached source lives on the branch (blank = not tracked)</span>
                <button class="browse-specs ghost" type="button" disabled title="Connect and pick a repository and branch first">browse…</button>
              </span>
              <div class="tree-slot tree-specs" popover="manual" hidden></div>
              <div class="spec-rows"></div>
            </div>
            <div class="row-actions"><button class="save-binding" type="button" disabled>Save binding</button></div>
            </div>
            <!-- Load lives with the binding: it syncs the working copy FROM the branch (a replace, not a git
                 merge). The forward round-trip is Commit; going backward is the History's roll back. -->
            <div class="load-section" hidden>
              <div class="row-actions">
                <button class="pull" type="button" disabled title="Load the bound branch's current contents into this working copy — replaces the document, bound sources, and scenarios (nothing partially applies)">⤓ Load from branch</button>
                <span class="hint">Load replaces this working copy's document, bound sources, and scenarios with the branch's current contents. To go back to an earlier commit instead, use the History below.</span>
              </div>
              <div class="load-result" hidden></div>
            </div>
          </fieldset>
          <div class="stage-hint bound-hint" hidden>Save the binding — Load and Commit work against the bound branch.</div>
          <fieldset class="roundtrip-section" hidden>
            <legend>Commit <a class="gh-link" target="_blank" rel="noopener" hidden style="margin-left:6px; font-size:11.5px;" title="Open the bound branch on GitHub">↗ Open on GitHub</a></legend>
            <label>Commit message <input class="c-message" type="text" placeholder="what changed"></label>
            <label class="check"><input class="c-pr" type="checkbox"> Open a draft pull request onto <arazzo-filter-input class="c-base" aria-label="Pull request base branch" placeholder="type to filter branches" title="Pick a repository first"></arazzo-filter-input></label>
            <div class="row-actions"><button class="commit" type="button" disabled title="Write the document, bound sources, and scenario files to the branch — authored as YOUR GitHub identity">⤒ Commit</button></div>
            <div class="result" hidden></div>
          </fieldset>
          <fieldset class="history-section" hidden>
            <legend>History — commits touching the bound document, newest first</legend>
            <div class="hist">
              <div class="head">
                <span class="title">Commits</span>
                <span class="count"></span>
                <span class="grow"></span>
                <span class="cmp-wrap">
                  <button class="hist-cmp ghost" type="button" aria-haspopup="menu" aria-expanded="false" disabled
                          title="Select one commit (vs the working copy or its predecessor) or two (between them)">⇆ Compare ▾</button>
                  <div class="cmp-menu" role="menu" hidden></div>
                </span>
                <button class="hist-rollback ghost" type="button" disabled
                        title="Select exactly one commit to roll the working copy back to it">↩ Roll back…</button>
              </div>
              <div class="commits" role="listbox" aria-multiselectable="true"></div>
              <button class="history-more ghost" type="button" hidden>More…</button>
            </div>
          </fieldset>
          <arazzo-workflow-compare class="compare"></arazzo-workflow-compare>
        </div>
      </div>`;

    this.$('.gh-connect').addEventListener('github-connected', () => this.renderBinding());
    this.$('.gh-connect').addEventListener('github-disconnected', () => this.renderBinding());
    this.$('.save-binding').addEventListener('click', (e) => { void this.runAction(e.currentTarget, () => this.saveBinding()); });
    this.$('.pull').addEventListener('click', async (e) => {
      const trigger = e.currentTarget; // captured before the await — currentTarget is null afterwards
      // Load is a REPLACE, not a git merge (§4.7): the branch's document, bound sources, and scenario
      // set overwrite the working copy. Say so before doing it — and point at History for going back.
      const sure = await this.$('.ask').ask({
        title: 'Load replaces this working copy',
        message: 'The branch’s document, bound sources, and scenario set replace what is here — local edits since the last save are lost. This is a load, not a merge. To go back to an earlier commit instead, use the History.',
        confirmLabel: 'Load & replace',
        danger: true,
      });
      if (sure) void this.runAction(trigger, () => this.pull());
    });
    this.$('.commit').addEventListener('click', (e) => { void this.runAction(e.currentTarget, () => this.commit()); });
    this.$('.c-message').addEventListener('input', () => this.updateActions());
    this.$('.b-repo').addEventListener('change', () => { void this.loadBranches(); });
    this.$('.b-branch').addEventListener('change', () => {
      if (this.$('.b-branch').value === NEW_BRANCH) {
        // The sentinel is an ACTION, not a branch: enter create mode and clear the field so its
        // token (`__new__`) never shows, then reveal + seed + focus the name input.
        this.enterCreateBranch();
      } else {
        this.closeNewBranch();
      }

      this.updateActions();
    });
    this.$('.nb-create').addEventListener('click', (e) => { void this.runAction(e.currentTarget, () => this.createBranch()); });
    this.$('.history-more').addEventListener('click', () => { void this.loadHistory({ more: true }); });
    this.$('.hist-cmp').addEventListener('click', () => this.toggleCompareMenu());
    this.$('.hist-rollback').addEventListener('click', (e) => {
      const trigger = e.currentTarget;
      const sel = this.selectedCommits();
      if (sel.length === 1) void this.rollbackTo(sel[0], trigger);
    });
    // The menu closes on any press outside its wrap — component-scoped, never document-level.
    this.shadowRoot.addEventListener('pointerdown', (e) => {
      if (!e.composedPath().some((n) => n instanceof Element && n.classList?.contains('cmp-wrap'))) {
        this.closeCompareMenu();
      }
    });
    this.wireTreeBrowser('.browse-path', '.tree-path', '.b-path', 'file');
    this.wireTreeBrowser('.browse-scenarios', '.tree-scenarios', '.b-scenarios', 'dir');
    // Picking a specs DIRECTORY tracks the attached sources automatically: every empty per-source
    // row fills as <dir>/<name>.json (still editable — the rows stay the truth).
    this.wireTreeBrowser('.browse-specs', '.tree-specs', null, 'dir', (path) => {
      for (const input of this.$$('.b-spec')) {
        if (!input.value.trim()) input.value = `${path.replace(/\/$/, '')}/${input.dataset.name}.json`;
      }

      this.updateActions();
    });
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
    const repos = session?.connected ? (session.repositories ?? []) : [];
    const input = this.$('.b-repo');
    const current = binding ? `${binding.owner}/${binding.repo}` : '';
    // The kit's filter combo (the shared picker design): the session's seed narrows as you type, an
    // owner-qualified query deepens through the server-side typeahead (reaching public repos the seed
    // never contains), and any visible owner/repo stays free-typable (the OAuth model).
    const repoItem = (r) => ({ value: r.fullName || `${r.owner}/${r.name}`, sub: [r.defaultBranch, r.private ? 'private' : ''].filter(Boolean).join(' · ') });
    const items = repos.map(repoItem);
    if (current && !items.some((i) => i.value === current)) items.push({ value: current });
    input.items = items;
    input.lookup = (q) => (q.includes('/') && this._client
      ? this._client.searchRepositories(q).then((r) => (r.repositories ?? []).map(repoItem)).catch(() => [])
      : []);
    input.value = current;
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

  /** @private — a browse… button toggles a LAZY repo tree (directories fetch on expand) whose
   *  pick lands in the field. Reuses <arazzo-git-tree>; the loader browses the bound repo+branch. */
  wireTreeBrowser(buttonSel, slotSel, fieldSel, mode, onPick) {
    this.$(buttonSel).addEventListener('click', () => {
      const slot = this.$(slotSel);
      if (this.isTreeOpen(slot)) { this.closeTree(slot); return; }
      const repoValue = this.$('.b-repo').value;
      const slash = repoValue.indexOf('/');
      const branch = this.branchValue();
      const tree = document.createElement('arazzo-git-tree');
      tree.mode = mode;
      if (mode === 'file') tree.pickableFile = (entry) => /\.(json|ya?ml)$/i.test(entry.name);
      tree.loader = async (path) => {
        const node = await this._client.browseRepo(repoValue.slice(0, slash), repoValue.slice(slash + 1), { path: path || undefined, ref: branch || undefined });
        return node.kind === 'dir' ? node.entries : [];
      };
      tree.addEventListener('picked', (e) => {
        if (fieldSel) this.$(fieldSel).value = e.detail.path;
        if (onPick) onPick(e.detail.path);
        this.closeTree(slot);
        this.updateActions();
      });
      slot.replaceChildren(tree);
      this.openTree(slot, this.$(buttonSel));
    });
  }

  /** @private Whether the browse dropdown is showing (popover-open, with a plain-hidden fallback). */
  isTreeOpen(slot) {
    return slot.matches?.(':popover-open') ?? !slot.hidden;
  }

  /** @private Position a floating dropdown panel below its anchor row (flipping above when the space
   *  below is tight), clamped to the viewport. Width tracks the anchor but honours a minimum. */
  placeDropdown(slot, anchor, minWidth = 0) {
    const r = anchor.getBoundingClientRect();
    const width = Math.max(r.width, minWidth);
    const left = Math.max(8, Math.min(r.left, window.innerWidth - width - 8));
    const below = window.innerHeight - r.bottom - 8;
    const above = r.top - 8;
    const placeAbove = below < 200 && above > below;
    slot.style.left = `${left}px`;
    slot.style.width = `${width}px`;
    slot.style.maxHeight = `${Math.min(340, Math.max(placeAbove ? above : below, 160))}px`;
    if (placeAbove) { slot.style.top = 'auto'; slot.style.bottom = `${window.innerHeight - r.top + 4}px`; }
    else { slot.style.bottom = 'auto'; slot.style.top = `${r.bottom + 4}px`; }
  }

  /** @private Show the browse tree as a floating dropdown in the top layer, anchored to its row so it
   *  overlays content rather than pushing it. Any other open browse tree is closed first. */
  openTree(slot, anchor) {
    this.$$('.tree-slot').forEach((s) => { if (s !== slot) this.closeTree(s); });
    this.placeDropdown(slot, anchor.closest('.pathrow') ?? anchor);
    // Clear the plain-hidden attribute BEFORE showing: while it is set, `.tree-slot[hidden]` forces
    // display:none even once the popover is open, so the tree would toggle state but never appear.
    slot.hidden = false;
    if (slot.showPopover && !slot.matches(':popover-open')) slot.showPopover();
    // Dismiss on Escape or a press outside the dropdown (a manual popover does not light-dismiss). The
    // listeners ride the shadow root so `e.target` is not retargeted to the host, and Escape closes the
    // dropdown WITHOUT bubbling to the modal dialog's own cancel.
    this._treeDismiss = (e) => {
      if (e.type === 'keydown') {
        if (e.key !== 'Escape') return;
        e.preventDefault();
        e.stopPropagation();
        this.closeTree(slot);
        return;
      }

      if (slot.contains(e.target) || anchor.contains(e.target)) return;
      this.closeTree(slot);
    };
    this.shadowRoot.addEventListener('keydown', this._treeDismiss, true);
    this.shadowRoot.addEventListener('pointerdown', this._treeDismiss, true);
  }

  /** @private Hide and empty a browse dropdown and drop its dismiss listeners. */
  closeTree(slot) {
    if (!slot) return;
    if (slot.hidePopover && slot.matches(':popover-open')) slot.hidePopover();
    slot.hidden = true;
    slot.replaceChildren();
    if (this._treeDismiss) {
      this.shadowRoot.removeEventListener('keydown', this._treeDismiss, true);
      this.shadowRoot.removeEventListener('pointerdown', this._treeDismiss, true);
      this._treeDismiss = null;
    }
  }

  /** @private Show the new-branch form as a floating dropdown anchored to the Branch field, so it
   *  overlays content rather than appearing inline and shifting the dialog around. Dismiss mirrors
   *  the browse trees (Escape or a press outside, riding the shadow root so the modal stays open). */
  openNewBranch() {
    const slot = this.$('.new-branch');
    const anchor = this.$('.b-branch');
    this.placeDropdown(slot, anchor, 300);
    slot.hidden = false;
    if (slot.showPopover && !slot.matches(':popover-open')) slot.showPopover();
    this._nbDismiss = (e) => {
      if (e.type === 'keydown') {
        if (e.key !== 'Escape') return;
        e.preventDefault();
        e.stopPropagation();
        this.closeNewBranch();
        return;
      }

      if (slot.contains(e.target) || anchor.contains(e.target)) return;
      this.closeNewBranch();
    };
    this.shadowRoot.addEventListener('keydown', this._nbDismiss, true);
    this.shadowRoot.addEventListener('pointerdown', this._nbDismiss, true);
  }

  /** @private Hide the new-branch dropdown, leave create mode, and drop its dismiss listeners. */
  closeNewBranch() {
    const slot = this.$('.new-branch');
    if (slot.hidePopover && slot.matches(':popover-open')) slot.hidePopover();
    slot.hidden = true;
    this._creatingBranch = false;
    if (this._nbDismiss) {
      this.shadowRoot.removeEventListener('keydown', this._nbDismiss, true);
      this.shadowRoot.removeEventListener('pointerdown', this._nbDismiss, true);
      this._nbDismiss = null;
    }
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
      sel.items = [];
      sel.value = '';
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
      const branchItem = (name) => ({ value: name, sub: name === list.defaultBranch ? 'default' : '' });
      sel.items = [
        // Pinned at the TOP so it is findable when a repo has very many branches (dotnet/runtime).
        { value: NEW_BRANCH, label: '＋ New branch…' },
        ...names.map(branchItem),
        ...(bound && !names.includes(bound) ? [{ value: bound, sub: 'not on remote' }] : []),
      ];
      sel.value = picked;
      sel.disabled = false;
      sel.title = 'The branch this working copy binds to';
      const base = this.$('.nb-base');
      base.items = names.map(branchItem);
      base.value = list.defaultBranch ?? names[0] ?? '';
      const prBase = this.$('.c-base');
      if (prBase) {
        const held = prBase.value;
        prBase.items = names.map(branchItem);
        prBase.value = names.includes(held) ? held : (list.defaultBranch ?? '');
        prBase.disabled = false;
        prBase.title = 'The branch the pull request targets';
      }
    } catch (err) {
      if (seq !== this._branchSeq) return;
      sel.disabled = true;
      sel.title = 'Branches could not be loaded';
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }

    this.closeNewBranch();
    this.updateActions();
  }

  // Enter "new branch" mode: the field is cleared (so the `__new__` sentinel token never shows), the
  // name is seeded from the working copy, the form drops as a dropdown, and the name is focused.
  enterCreateBranch() {
    this._creatingBranch = true;
    this.$('.b-branch').value = '';
    if (!this.$('.nb-name').value) {
      const slug = (this._workingCopy?.name ?? 'my-flow').toLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/^-|-$/g, '');
      this.$('.nb-name').value = `feature/${slug || 'my-flow'}`;
    }

    this.openNewBranch();
    const nbName = this.$('.nb-name');
    nbName.focus();
    nbName.select();
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
      this.closeNewBranch();
      this.updateActions();
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  updateActions() {
    const connected = !!this.$('.gh-connect').session?.connected;
    const formed = this.$('.b-repo').value && this.branchValue() && this.$('.b-path').value.trim();
    const bound = !!this._workingCopy?.gitBinding;

    // PROGRESSIVE disclosure: each stage appears when the previous one is satisfied — a wall of
    // disabled controls explains nothing; a staged panel narrates the journey.
    const picked = !!(this.$('.b-repo').value && this.branchValue());
    this.$('.connect-hint').hidden = connected;
    this.$('.binding-section').hidden = !connected;
    this.$('.paths-hint').hidden = picked;
    this.$('.paths-section').hidden = !picked;
    this.$('.bound-hint').hidden = !connected || bound;
    this.$('.load-section').hidden = !bound;   // Load lives with the binding, and only once bound.
    this.$('.roundtrip-section').hidden = !bound;
    this.$('.history-section').hidden = !connected || !bound;
    if (connected && bound) void this.loadHistory();

    // A jump to the bound branch on GitHub — encode each branch segment but keep the slashes so
    // `feature/foo` lands correctly (a whole-string encode would turn `/` into %2F and 404).
    const ghLink = this.$('.gh-link');
    const b = this._workingCopy?.gitBinding;
    if (bound && b?.owner && b?.repo && b?.branch) {
      const branchPath = b.branch.split('/').map(encodeURIComponent).join('/');
      ghLink.href = `https://github.com/${encodeURIComponent(b.owner)}/${encodeURIComponent(b.repo)}/tree/${branchPath}`;
      ghLink.hidden = false;
    } else {
      ghLink.hidden = true;
    }

    // A disabled control carries its reason — nothing greys out silently.
    const save = this.$('.save-binding');
    save.disabled = !this._workingCopy || !formed;
    save.title = save.disabled ? 'Pick a repository and fill in the branch and document path first' : 'Store this binding on the working copy';
    const pull = this.$('.pull');
    pull.disabled = !connected || !bound;
    pull.title = pull.disabled
      ? (!connected ? 'Connect GitHub first' : 'Save a binding first — Load reads from the bound branch')
      : 'Load the bound branch into this working copy — replaces the document, bound sources, and scenarios (nothing partially applies)';
    for (const sel of ['.browse-path', '.browse-scenarios', '.browse-specs']) {
      const browse = this.$(sel);
      if (browse) {
        browse.disabled = !connected || !this.$('.b-repo').value || !this.branchValue();
        browse.title = browse.disabled ? 'Connect and pick a repository and branch first' : 'Browse the branch (directories load as you expand — large trees never load whole)';
      }
    }

    const commit = this.$('.commit');
    commit.disabled = !connected || !bound || !this.$('.c-message').value.trim();
    commit.title = commit.disabled
      ? (!connected ? 'Connect GitHub first' : !bound ? 'Save a binding first — Commit writes to the bound branch' : 'Enter a commit message')
      : 'Write the document, bound sources, and scenario files to the branch — authored as YOUR GitHub identity';
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
        ? 'The working copy changed underneath — reopen the working copy to rebind.'
        : err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }
  }

  async pull() {
    this.clearError();
    try {
      // Pull is a discard-and-REPLACE, so operate on the CURRENT stored copy. The panel captured its
      // etag when it opened, but the canvas stays editable beside it — an edit since then bumped the
      // working copy's etag, and pulling against the stale one 409s ("re-fetch and pull against the
      // fresh state") and never reverts. 'pull-starting' lets the host cancel a pending autosave
      // synchronously first; then re-read for the live etag.
      this.emit('pull-starting');
      this._workingCopy = await this._client.getWorkingCopy(this._workingCopy.id);
      const pulled = await this._client.pullWorkingCopy(this._workingCopy.id, { expectedEtag: this._workingCopy.etag });
      this._workingCopy = pulled;
      this.renderBinding();
      const result = this.$('.load-result');
      result.hidden = false;
      result.innerHTML = `<span>Loaded from <strong>${escapeHtml(`${pulled.gitBinding.owner}/${pulled.gitBinding.repo}@${pulled.gitBinding.branch}`)}</strong>.</span>`;
      this.emit('pulled', { workingCopy: pulled });
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }

    this.updateActions();
  }

  async commit() {
    this.clearError();
    const message = this.$('.c-message').value.trim();
    const pullRequest = this.$('.c-pr').checked ? { base: this.$('.c-base').value.trim() || 'main', draft: true } : undefined;
    try {
      const result = await this._client.commitWorkingCopy(this._workingCopy.id, { message, ...(pullRequest ? { pullRequest } : {}) });
      const box = this.$('.result');
      box.hidden = false;
      box.innerHTML = `
        <span>Committed as <strong>${escapeHtml(this.$('.gh-connect').session?.login ?? 'you')}</strong>:</span>
        ${result.files.map((f) => `<span class="file">${escapeHtml(f.path)}</span>`).join('')}
        ${result.pullRequest ? `<span>Pull request: <a href="${escapeHtml(result.pullRequest.url)}" target="_blank" rel="noopener">#${escapeHtml(String(result.pullRequest.number))}</a></span>` : ''}`;
      this.emit('committed', { result });
      this._historyKey = ''; // the branch advanced — the history list is stale
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
      this.emit('error', { problem: err.problem, error: err });
    }

    this.updateActions();
  }

  // ── History (snag 9): browse the bound branch's commits, compare any of them side-by-side
  // with the current state, and roll back (a danger-confirmed pull at that commit). ─────────────

  /** @private — the bound (owner, repo, branch, path), or null while unbound. */
  historyScope() {
    const binding = this._workingCopy?.gitBinding;
    return binding?.owner && binding?.repo && binding?.branch && binding?.path ? binding : null;
  }

  /** @private — loads one page of the bound branch's history; re-entry with the same scope is a
   *  no-op (updateActions fires often), `more` appends the next page. */
  async loadHistory({ more = false } = {}) {
    const binding = this.historyScope();
    if (!binding) return;
    const key = `${binding.owner}/${binding.repo}@${binding.branch}:${binding.path}`;
    if (!more && key === this._historyKey) return;
    const seq = ++this._historySeq;
    const page = more ? this._historyPage + 1 : 1;
    try {
      const list = await this._client.listRepoCommits(binding.owner, binding.repo, {
        sha: binding.branch, path: binding.path, page, perPage: 10,
      });
      if (seq !== this._historySeq) return;
      this._historyKey = key;
      this._historyPage = page;
      if (page === 1) {
        this._historyCommits = [];
        this._historySelection = [];
      }

      this._historyCommits.push(...list.commits);
      this._historyHasMore = !!list.hasMore;
      this.renderHistory();
    } catch (err) {
      if (seq !== this._historySeq) return;
      this._historyCommits = [];
      this._historySelection = [];
      this._historyHasMore = false;
      this.renderHistory(`History could not be loaded — ${escapeHtml(err.problem?.detail || err.problem?.title || err.message)}`);
    }
  }

  /** @private — the accumulated rows + the "More…" footer; selection survives paging (keyed by sha). */
  renderHistory(errorHtml = null) {
    const rows = this.$('.commits');
    rows.replaceChildren();
    if (errorHtml) {
      rows.innerHTML = `<span class="history-empty">${errorHtml}</span>`;
    } else if (this._historyCommits.length === 0) {
      rows.innerHTML = '<span class="history-empty">No commits touch the bound document yet — the first commit starts the history.</span>';
    } else {
      for (const commit of this._historyCommits) rows.appendChild(this.renderCommit(commit));
    }

    this.$('.history-more').hidden = !this._historyHasMore;
    this.updateHistoryActions();
  }

  /** @private — one selectable commit row: message + sha·author·date. Selection is click-to-toggle,
   *  MAX TWO — picking a third evicts the earliest pick, so the gesture never dead-ends. */
  renderCommit(commit) {
    const row = document.createElement('div');
    row.className = 'hist-commit'; // NOT 'commit' — that class is the round-trip section's commit button
    row.setAttribute('role', 'option');
    row.setAttribute('data-sha', commit.sha);
    row.setAttribute('aria-selected', String(this._historySelection.includes(commit.sha)));
    const when = commit.date ? new Date(commit.date).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '';
    row.innerHTML = `
      <span class="msg" title="${escapeHtml(commit.message ?? '')}">${escapeHtml(commit.message ?? '(no message)')}</span>
      <span class="meta">${escapeHtml(commit.sha.slice(0, 7))}${commit.author ? ` · ${escapeHtml(commit.author)}` : ''}${when ? ` · ${escapeHtml(when)}` : ''}</span>`;
    row.addEventListener('click', () => this.toggleCommitSelection(commit.sha));
    return row;
  }

  /** @private */
  toggleCommitSelection(sha) {
    const at = this._historySelection.indexOf(sha);
    if (at >= 0) {
      this._historySelection.splice(at, 1);
    } else {
      this._historySelection.push(sha);
      if (this._historySelection.length > 2) this._historySelection.shift();
    }

    for (const row of this.$$('.hist-commit')) {
      row.setAttribute('aria-selected', String(this._historySelection.includes(row.getAttribute('data-sha'))));
    }

    this.updateHistoryActions();
  }

  /** @private — the selected commits, in LIST order (newest first), never more than two. */
  selectedCommits() {
    return this._historyCommits.filter((c) => this._historySelection.includes(c.sha));
  }

  /** @private — header state: the count, the Compare menu (1 selection: vs working copy / vs
   *  predecessor; 2: between them), and Roll back enabled only at EXACTLY one selection. */
  updateHistoryActions() {
    const n = this._historySelection.length;
    const count = this.$('.hist .count');
    count.textContent = `${this._historyCommits.length}${this._historyHasMore ? '+' : ''} commit${this._historyCommits.length === 1 ? '' : 's'}${n ? ` · ${n} selected` : ''}`;
    this.$('.hist-cmp').disabled = n === 0;
    this.$('.hist-rollback').disabled = n !== 1;
    this.closeCompareMenu();
  }

  /** @private */
  toggleCompareMenu() {
    const menu = this.$('.cmp-menu');
    if (!menu.hidden) {
      this.closeCompareMenu();
      return;
    }

    const sel = this.selectedCommits();
    menu.replaceChildren();
    if (sel.length === 1) {
      const commit = sel[0];
      const idx = this._historyCommits.findIndex((c) => c.sha === commit.sha);
      const predecessor = idx >= 0 ? this._historyCommits[idx + 1] : undefined;
      menu.append(
        this.compareMenuItem('With the working copy', () => this.compareCommit(commit)),
        predecessor
          ? this.compareMenuItem(`With its predecessor (${predecessor.sha.slice(0, 7)})`, () => this.compareCommits(predecessor, commit))
          : this.compareMenuItem(
              this._historyHasMore ? 'With its predecessor — load More… first' : 'With its predecessor — none (first commit)',
              null),
      );
    } else if (sel.length === 2) {
      const [newer, older] = sel; // list order is newest first
      menu.append(this.compareMenuItem(
        `${older.sha.slice(0, 7)} ↔ ${newer.sha.slice(0, 7)} (older → newer)`,
        () => this.compareCommits(older, newer)));
    }

    menu.hidden = false;
    this.$('.hist-cmp').setAttribute('aria-expanded', 'true');
  }

  /** @private */
  compareMenuItem(label, action) {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'menu-item';
    item.setAttribute('role', 'menuitem');
    item.textContent = label;
    if (action) {
      item.addEventListener('click', () => {
        this.closeCompareMenu();
        void action();
      });
    } else {
      item.disabled = true;
    }

    return item;
  }

  /** @private */
  closeCompareMenu() {
    this.$('.cmp-menu').hidden = true;
    this.$('.hist-cmp').setAttribute('aria-expanded', 'false');
  }

  /** The reusable compare dialog, so the host can `refresh()` it after applying a merge Take (§6.4). */
  get compareDialog() { return this.$('.compare'); }

  /** @private — fetch the bound document AT the commit and open the reusable side-by-side visualizer:
   *  current state on the left (the MERGE TARGET), the commit on the right. The working-copy document is
   *  read from the LIVE model via `documentSource` (§6.4 merge-session integrity) — the panel's own
   *  `_workingCopy` snapshot goes stale under autosave, and a merge must compute Takes against the document
   *  the host actually mutates. */
  async compareCommit(commit) {
    this.clearError();
    const binding = this.historyScope();
    if (!binding) return;
    try {
      const node = await this._client.browseRepo(binding.owner, binding.repo, { path: binding.path, ref: commit.sha });
      const historic = decodeJsonFile(node);
      const live = this.documentSource ? this.documentSource() : (this._workingCopy?.document ?? {});
      this.$('.compare').open({
        left: { label: `Working copy (current) — ${this._workingCopy?.name ?? ''}`, document: live, mergeTarget: !!this.documentSource },
        right: { label: `${commit.sha.slice(0, 7)} — ${commit.message ?? ''}`, document: historic },
        // Centre on the workflow the author is EDITING when the host says (workflowIdSource), not
        // blindly the document's first; each side falls back to its own content when it lacks the id.
        workflowId: this.workflowIdSource?.() ?? live?.workflows?.[0]?.workflowId,
      });
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  /** @private — two commits side-by-side, OLDER on the left so the diff reads as what changed over
   *  time (added = present only on the right). Read-only: neither side is the merge target. */
  async compareCommits(older, newer) {
    this.clearError();
    const binding = this.historyScope();
    if (!binding) return;
    try {
      const [before, after] = await Promise.all([
        this._client.browseRepo(binding.owner, binding.repo, { path: binding.path, ref: older.sha }),
        this._client.browseRepo(binding.owner, binding.repo, { path: binding.path, ref: newer.sha }),
      ]);
      const olderDoc = decodeJsonFile(before);
      const newerDoc = decodeJsonFile(after);
      this.$('.compare').open({
        left: { label: `${older.sha.slice(0, 7)} — ${older.message ?? ''}`, document: olderDoc },
        right: { label: `${newer.sha.slice(0, 7)} — ${newer.message ?? ''}`, document: newerDoc },
        workflowId: newerDoc?.workflows?.[0]?.workflowId,
      });
    } catch (err) {
      this.showError(err.problem?.detail || err.problem?.title || err.message);
    }
  }

  /** @private — the rollback IS a pull at the commit's ref: danger-confirmed, etag-guarded, and
   *  recorded on the branch by the NEXT commit (the binding never changes). */
  async rollbackTo(commit, trigger) {
    const sure = await this.$('.ask').ask({
      title: `Roll back to ${commit.sha.slice(0, 7)}?`,
      message: `“${commit.message ?? commit.sha}” replaces the working copy's document${this._workingCopy?.gitBinding?.specPaths ? ', bound sources,' : ''} and scenario set — local edits since the last commit are lost. The branch itself is untouched until you commit the rollback.`,
      confirmLabel: 'Roll back & replace',
      danger: true,
    });
    if (!sure) return;
    this.clearError();
    // The confirm is modal, so re-entry is already blocked; runAction just spins the trigger for the
    // network part (never during the dialog), and the inner handler owns its own error display.
    await this.runAction(trigger, async () => {
      try {
        // Same discard-and-replace semantics as pull() — refresh to the live etag first (a canvas edit
        // beside the open panel would otherwise 409 the rollback and leave the document unchanged).
        this.emit('pull-starting');
        this._workingCopy = await this._client.getWorkingCopy(this._workingCopy.id);
        const pulled = await this._client.pullWorkingCopy(this._workingCopy.id, { expectedEtag: this._workingCopy.etag, ref: commit.sha });
        this._workingCopy = pulled;
        this.renderBinding();
        const result = this.$('.result');
        result.hidden = false;
        result.innerHTML = `<span>Rolled back to <strong>${escapeHtml(commit.sha.slice(0, 7))}</strong> — commit to record it on <strong>${escapeHtml(pulled.gitBinding.branch)}</strong>.</span>`;
        this.emit('pulled', { workingCopy: pulled });
      } catch (err) {
        this.showError(err.problem?.detail || err.problem?.title || err.message);
        this.emit('error', { problem: err.problem, error: err });
      }

      this.updateActions();
    });
  }
}

define('arazzo-git-dialog', ArazzoGitDialog);
export { ArazzoGitDialog };