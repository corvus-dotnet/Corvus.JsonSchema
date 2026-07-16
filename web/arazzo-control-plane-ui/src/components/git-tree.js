// <arazzo-git-tree> — a REUSABLE lazy tree browser: children are fetched only when a directory
// expands (large/deep repositories never load whole), fetched directories are cached, and picking
// emits the path. The loader abstracts the backend (GitHub contents, or anything tree-shaped).
//
//   const tree = document.createElement('arazzo-git-tree');
//   tree.loader = async (path) => [{ name, path, type: 'dir'|'file' }, …];  // one directory level
//   tree.mode = 'file';                    // what PICKING means: 'file' (default) or 'dir'
//   tree.pickableFile = (entry) => entry.name.endsWith('.json');   // optional file filter
//   tree.addEventListener('picked', (e) => e.detail.path);
//
// Properties : .loader, .mode, .pickableFile
// Events     : picked {path, entry} · error {error}

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';

class ArazzoGitTree extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._cache = new Map(); // path → entries (a directory fetched once)
    this.mode = 'file';
  }

  connectedCallback() {
    this.render();
  }

  /** Reset and reload from the root (call after the loader's target changes). */
  reload() {
    this._cache.clear();
    this.render();
  }

  /** @private */
  render() {
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; font-size: 12px; }
        .tree { max-height: 240px; overflow: auto; border: 1px solid var(--_border); border-radius: 6px; padding: 4px; }
        ul { list-style: none; margin: 0; padding-left: 14px; }
        ul.root { padding-left: 0; }
        li { min-width: 0; }
        button.entry { display: flex; gap: 6px; align-items: center; width: 100%; text-align: left; border: none;
                       background: none; color: inherit; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace;
                       padding: 2px 4px; cursor: pointer; border-radius: 4px; min-width: 0; }
        button.entry:hover { background: var(--_surface); }
        button.entry:disabled { opacity: 0.45; cursor: default; }
        button.entry .name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .twist { width: 1em; flex-shrink: 0; color: var(--_muted); }
        .spin { color: var(--_muted); font-size: 11px; padding-left: 20px; }
        .pick-dir { font-size: 10.5px; padding: 0 6px; flex-shrink: 0; }
        .mkdir .name { color: var(--_muted); }
        .mkdir-name { font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; padding: 2px 6px; border: 1px solid var(--_border); border-radius: 4px; background: var(--_bg); color: inherit; width: 100%; box-sizing: border-box; }
      </style>
      <div class="tree"><ul class="root"></ul></div>`;
    void this.renderLevel(this.$('ul.root'), '');
  }

  /** @private — one directory level, fetched lazily and cached. */
  async renderLevel(listEl, path) {
    let entries = this._cache.get(path);
    if (!entries) {
      listEl.innerHTML = '<li class="spin">loading…</li>';
      try {
        entries = await (this.loader?.(path) ?? []);
        this._cache.set(path, entries);
      } catch (error) {
        listEl.innerHTML = '<li class="spin">could not load</li>';
        this.emit('error', { error });
        return;
      }
    }

    listEl.replaceChildren();
    const sorted = [...entries].sort((a, b) => (a.type === b.type ? a.name.localeCompare(b.name) : a.type === 'dir' ? -1 : 1));
    for (const entry of sorted) {
      const li = document.createElement('li');
      const row = document.createElement('button');
      row.type = 'button';
      row.className = 'entry';
      const isDir = entry.type === 'dir';
      const filePickable = !isDir && this.mode === 'file' && (this.pickableFile?.(entry) ?? true);
      row.disabled = !isDir && !filePickable;
      if (row.disabled) row.title = 'Not pickable here';
      row.innerHTML = `<span class="twist">${isDir ? '▸' : ''}</span><span class="name">${isDir ? '📁' : '📄'} ${escapeHtml(entry.name)}</span>`;

      if (isDir) {
        const children = document.createElement('ul');
        children.hidden = true;
        let expanded = false;
        row.addEventListener('click', () => {
          expanded = !expanded;
          row.querySelector('.twist').textContent = expanded ? '▾' : '▸';
          children.hidden = !expanded;
          if (expanded && !children.childElementCount) void this.renderLevel(children, entry.path);
        });
        if (this.mode === 'dir') {
          const pick = document.createElement('button');
          pick.type = 'button';
          pick.className = 'ghost pick-dir';
          pick.textContent = 'pick';
          pick.title = 'Pick this directory';
          pick.addEventListener('click', (e) => { e.stopPropagation(); this.emit('picked', { path: entry.path, entry }); });
          row.append(pick);
        }

        li.append(row, children);
      } else {
        row.addEventListener('click', () => { if (filePickable) this.emit('picked', { path: entry.path, entry }); });
        li.append(row);
      }

      listEl.append(li);
    }

    if (!sorted.length) listEl.innerHTML = '<li class="spin">(empty)</li>';

    // In dir mode a level offers "+ new directory" — a VIRTUAL node (git has no empty
    // directories; the path materialises when something commits into it).
    if (this.mode === 'dir') {
      const li = document.createElement('li');
      const add = document.createElement('button');
      add.type = 'button';
      add.className = 'entry mkdir';
      add.innerHTML = '<span class="twist"></span><span class="name">+ new directory…</span>';
      add.title = 'Name a directory here — it is created when the binding first commits into it';
      add.addEventListener('click', () => {
        const input = document.createElement('input');
        input.type = 'text';
        input.placeholder = 'directory-name';
        input.className = 'mkdir-name';
        li.replaceChildren(input);
        input.focus();
        const settle = () => {
          const name = input.value.trim().replace(/^\/+|\/+$/g, '');
          if (!name) { li.replaceChildren(add); return; }
          const full = path ? `${path}/${name}` : name;
          this.emit('picked', { path: full, entry: { name, path: full, type: 'dir', virtual: true } });
        };
        input.addEventListener('keydown', (e) => { if (e.key === 'Enter') settle(); if (e.key === 'Escape') li.replaceChildren(add); });
        input.addEventListener('blur', settle);
      });
      li.append(add);
      listEl.append(li);
    }
  }
}

define('arazzo-git-tree', ArazzoGitTree);
export { ArazzoGitTree };
