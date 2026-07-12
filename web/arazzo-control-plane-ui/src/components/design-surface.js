// <arazzo-design-surface> — the workflow designer's diagram area: an editable, debuggable SVG
// rendering of a projected workflow graph (design: workflow-designer-design.md §6).
//
//   import { projectWorkflow } from '../workflow-graph.js';
//   const surface = document.querySelector('arazzo-design-surface');
//   surface.graph = projectWorkflow(doc, 'place-order');
//
// Layers (§6.3): this component is only the *renderer* (keyed SVG build from the graph projection)
// and the *interaction* state machine (pan · zoom · drag-node · draw-edge · select). The projection
// and layout are pure siblings (src/workflow-graph.js, src/workflow-layout.js). Shadow-DOM safety
// is by construction: pointer capture + listeners on this shadow root only, coordinates via our own
// view transform — `document.elementFromPoint` and document-level listeners are deliberately absent.
//
// Properties : .graph (projection output), .layoutEngine (fn(graph) → positions; default built-in),
//              .layoutOverrides (manual {id:{x,y}}, survives re-layout), .selection
//              ({type:'node'|'edge'|'defaults', id?}|null), .breakpoints (iterable of stepIds),
//              .debugState ({active?, steps?: {id: 'done-success'|'done-failure'|'skipped'},
//              edges?: [edgeId]}|null)
// Attributes : readonly (mutation gestures off; pan/zoom/select/breakpoints still work)
// Events     : selection-changed {selection}, layout-changed {overrides}, entry-changed
//              {stepId} (a drag from the start node's entry port onto a step — the host reorders
//              that step to the FRONT of the steps array, because in Arazzo the entry point IS
//              steps[0]; the projection then re-derives the start edge), edge-created
//              {from, to, kind}, breakpoint-toggled {stepId, enabled}, node-activated {stepId},
//              delete-requested {selection},
//              edge-retargeted {id, actionName, from, kind, to} (the selected action edge's
//              arrowhead dragged onto another node/the end terminal — the host rewrites the action)
// Methods    : fit() — zoom/centre the whole graph into view.

import { ArazzoElement, SHARED_CSS, escapeHtml, define } from './base.js';
import { layoutGraph, NODE_WIDTH, NODE_HEIGHT } from '../workflow-layout.js';
import { START_ID } from '../workflow-graph.js';

const PSEUDO_R = 17; // radius of the start/end pseudo-node circles

const SVG = 'http://www.w3.org/2000/svg';
const PORT_KINDS = { 'port-success': 'success', 'port-failure': 'failure', 'port-entry': 'entry' };
const KIND_BADGE = { operation: 'OP', channel: 'CH', workflow: 'WF', unknown: '?' };

class ArazzoDesignSurface extends ArazzoElement {
  constructor() {
    super();
    /** @private */ this._graph = null;
    /** @private */ this._layoutEngine = layoutGraph;
    /** @private */ this._overrides = {};
    /** @private */ this._positions = {};
    /** @private */ this._selection = null;
    /** @private */ this._breakpoints = new Set();
    /** @private */ this._debugState = null;
    /** @private */ this._view = { tx: 40, ty: 40, k: 1 };
    /** @private */ this._gesture = null; // { type: 'pan'|'drag'|'draw', ... }
    /** @private */ this._fitted = false;
  }

  connectedCallback() {
    if (!this._built) this.renderShell();
    if (this._graph) this.renderGraph();
  }

  get readonly() { return this.hasAttribute('readonly'); }

  /** The projected graph (see projectWorkflow). Setting it re-lays-out and re-renders. */
  get graph() { return this._graph; }
  set graph(value) {
    this._graph = value;
    this._relayout();
    if (this.isConnected) { if (!this._built) this.renderShell(); this.renderGraph(); }
  }

  /** The layout function `(graph, opts) → {id: {x,y}}`; inject a dagre adapter here if wanted. */
  get layoutEngine() { return this._layoutEngine; }
  set layoutEngine(fn) {
    this._layoutEngine = typeof fn === 'function' ? fn : layoutGraph;
    if (this._graph) { this._relayout(); this.renderGraph(); }
  }

  /** Manual node positions (designerState); merged over the engine's output. */
  get layoutOverrides() { return { ...this._overrides }; }
  set layoutOverrides(value) {
    this._overrides = { ...(value || {}) };
    if (this._graph) { this._relayout(); this.renderGraph(); }
  }

  /** Current selection: {type:'node'|'edge'|'defaults', id?} | null. */
  get selection() { return this._selection; }
  set selection(value) {
    this._selection = value || null;
    this._applySelection();
  }

  /** Breakpointed stepIds. */
  get breakpoints() { return new Set(this._breakpoints); }
  set breakpoints(value) {
    this._breakpoints = new Set(value || []);
    this._applyBreakpoints();
  }

  /** Debug overlay: {active?, steps?: {stepId: status}, edges?: [edgeId]} | null. */
  get debugState() { return this._debugState; }
  set debugState(value) {
    this._debugState = value || null;
    this._applyDebug();
  }

  /** The current world positions of every node — hand back as `layoutOverrides` before replacing
   *  `.graph` to keep the canvas stable across a re-projection (no surprise reflow on edit). */
  get positions() { return { ...this._positions }; }

  /** Fit the whole graph into the viewport with a margin. */
  fit() {
    const svg = this.$('svg');
    if (!svg || !this._graph?.nodes.length) return;
    const xs = this._graph.nodes.map((n) => this._positions[n.id]?.x ?? 0);
    const ys = this._graph.nodes.map((n) => this._positions[n.id]?.y ?? 0);
    const minX = Math.min(...xs) - 40;
    const minY = Math.min(...ys) - 40;
    const maxX = Math.max(...xs) + NODE_WIDTH + 240; // room for the defaults card / exit chips
    const maxY = Math.max(...ys) + NODE_HEIGHT + 40;
    const box = svg.getBoundingClientRect();
    if (!box.width || !box.height) return;
    const k = Math.min(box.width / (maxX - minX), box.height / (maxY - minY), 1.5);
    this._view = { k, tx: -minX * k + (box.width - (maxX - minX) * k) / 2, ty: -minY * k + (box.height - (maxY - minY) * k) / 2 };
    this._applyView();
  }

  // ── Layout ────────────────────────────────────────────────────────────────────────────────────

  /** @private */
  _relayout() {
    if (!this._graph) { this._positions = {}; return; }
    const computed = this._layoutEngine(this._graph) || {};
    this._positions = {};
    for (const node of this._graph.nodes) {
      this._positions[node.id] = this._overrides[node.id] || computed[node.id] || { x: 0, y: 0 };
    }
  }

  // ── Shell + rendering ─────────────────────────────────────────────────────────────────────────

  /** @private */
  renderShell() {
    this._built = true;
    this.shadowRoot.innerHTML = `
      <style>
        ${SHARED_CSS}
        :host { display: block; min-height: 320px; }
        svg { display: block; width: 100%; height: 100%; min-height: 320px; background: var(--_surface);
              border: 1px solid var(--_border); border-radius: var(--_radius); cursor: grab;
              touch-action: none; user-select: none; -webkit-user-select: none; }
        svg:focus-visible { outline: 2px solid var(--_accent); outline-offset: 1px; }
        svg.panning { cursor: grabbing; }

        /* Nodes */
        .node { cursor: pointer; }
        .node .card { fill: var(--_bg); stroke: var(--_border); stroke-width: 1.25; rx: 10; }
        .node:hover .card { stroke: var(--_accent); }
        .node.selected rect.card, .node.selected circle.card { stroke: var(--_accent); stroke-width: 2; }
        .node .label { font: 600 13px var(--_font); fill: var(--_text); }
        .node .sub { font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; fill: var(--_muted); }
        .node .kbadge { rx: 4; }
        .node .kbadge-text { font: 700 9px var(--_font); fill: #fff; }
        .node.kind-operation .kbadge { fill: var(--_accent); }
        .node.kind-channel .kbadge { fill: var(--arazzo-kind-channel, #8250df); }
        .node.kind-workflow .kbadge { fill: var(--arazzo-kind-workflow, #0d8a8a); }
        .node.kind-unknown .kbadge { fill: var(--_danger); }
        .chip { font: 10px var(--_font); fill: var(--_muted); }
        .chip.ghost { font-style: italic; opacity: 0.75; }
        .chip.end { fill: var(--_text); font-weight: 700; }

        /* Start/end pseudo-nodes (projection-only; never in the document) */
        .node.pseudo circle.card { fill: var(--_surface); stroke-width: 2; }
        .node.kind-start circle.card { stroke: var(--_accent); }
        .node.kind-end circle.card { stroke: var(--_muted); }
        .node.pseudo .glyph { font: 700 10px var(--_font); fill: var(--_accent); }
        .node.pseudo .inner { fill: var(--_muted); }
        .node.pseudo .plabel { font: 700 10px var(--_font); fill: var(--_muted);
                               text-transform: uppercase; letter-spacing: 0.06em; }

        /* Breakpoint dot — solid card-coloured fill so edges never show through it */
        .bp { fill: var(--_bg); stroke: var(--_muted); stroke-width: 1; cursor: pointer; }
        .bp:hover { stroke: var(--_danger); }
        .node.bp-on .bp { fill: var(--_danger); stroke: var(--_danger); }

        /* Ports */
        .port { fill: var(--_bg); stroke-width: 1.5; cursor: crosshair; }
        .port-success { stroke: var(--arazzo-status-completed, #2a8a4a); }
        .port-failure { stroke: var(--arazzo-status-faulted, #d4351c); }
        .port-entry { stroke: var(--_accent); }
        .port:hover { stroke-width: 3; }
        :host([readonly]) .port { display: none; }

        /* Edges */
        .edge .line { fill: none; stroke-width: 1.75; }
        .edge .hit { fill: none; stroke: transparent; stroke-width: 14; cursor: pointer; }
        .edge.selected .line { stroke-width: 3; }
        .edge-seq .line { stroke: var(--_muted); stroke-dasharray: 4 4; opacity: 0.8; }
        .edge-success .line { stroke: var(--arazzo-status-completed, #2a8a4a); }
        .edge-failure .line { stroke: var(--arazzo-status-faulted, #d4351c); }
        .edge.reusable .line { stroke-dasharray: 8 3; }
        /* An action after a criteria-less catch-all can never fire (first-match-wins). */
        .edge.unreachable { opacity: 0.35; }
        .edge.unreachable .elabel { text-decoration: line-through; }
        .elabel { paint-order: stroke; stroke: var(--_bg); stroke-width: 3px; stroke-linejoin: round;
                  font: 10px ui-monospace, SFMono-Regular, Menlo, monospace; fill: var(--_muted);
                  paint-order: stroke; stroke: var(--_surface); stroke-width: 3; }
        .elabel.ghost { font-style: italic; opacity: 0.8; }
        marker path { fill: var(--_muted); }
        marker.m-success path { fill: var(--arazzo-status-completed, #2a8a4a); }
        marker.m-failure path { fill: var(--arazzo-status-faulted, #d4351c); }

        /* Rubber band while drawing an edge (coloured by the edge kind being drawn) */
        .rubber { fill: none; stroke: var(--_accent); stroke-width: 2; stroke-dasharray: 5 4; pointer-events: none; }
        .retarget { fill: var(--_bg); stroke: var(--_accent); stroke-width: 2; cursor: grab; }
        .retarget:hover { fill: var(--_accent); }
        .rubber.rubber-success { stroke: var(--arazzo-status-completed, #2a8a4a); }
        .rubber.rubber-failure { stroke: var(--arazzo-status-faulted, #d4351c); }
        .node.link-target .card { stroke: var(--_accent); stroke-width: 2.5; }

        /* Exit chips (goto another workflow) */
        .exit-chip rect { fill: var(--_surface); stroke: var(--_border); stroke-dasharray: 3 3; rx: 12; }
        .exit-chip text { font: 11px var(--_font); fill: var(--_muted); }
        .exit-chip { cursor: pointer; }

        /* Workflow-defaults card (the inherited layer) */
        .defaults rect.card { fill: var(--_bg); stroke: var(--_border); stroke-dasharray: 5 3; rx: 10; }
        .defaults.selected rect.card { stroke: var(--_accent); stroke-width: 2; stroke-dasharray: none; }
        .defaults .title { font: 700 11px var(--_font); fill: var(--_muted); letter-spacing: 0.04em; }
        .defaults .row { font: 11px ui-monospace, SFMono-Regular, Menlo, monospace; fill: var(--_text); }
        .defaults { cursor: pointer; }

        /* Debug overlay */
        svg.debugging .node:not(.st-active):not(.st-done-success):not(.st-done-failure) { opacity: 0.45; }
        svg.debugging .edge { opacity: 0.3; }
        svg.debugging .edge.lit { opacity: 1; }
        /* Element-qualified so these beat the pseudo-node base strokes (.node.kind-end circle.card). */
        .node.st-done-success rect.card, .node.st-done-success circle.card { stroke: var(--arazzo-status-completed, #2a8a4a); stroke-width: 2; }
        .node.st-done-failure rect.card, .node.st-done-failure circle.card { stroke: var(--arazzo-status-faulted, #d4351c); stroke-width: 2; }
        .node.st-skipped { opacity: 0.45; }
        .node.st-active { opacity: 1; }
        .node.st-active rect.card, .node.st-active circle.card { stroke: var(--_accent); stroke-width: 2.5; animation: arazzo-pulse 1.1s ease-in-out infinite; }
        @keyframes arazzo-pulse {
          0%, 100% { stroke-opacity: 1; }
          50% { stroke-opacity: 0.35; }
        }
        @media (prefers-reduced-motion: reduce) { .node.st-active .card { animation: none; } }
        .empty { padding: 40px 12px; }
      </style>
      <svg part="surface" tabindex="0" role="application" aria-label="Workflow design surface">
        <defs>
          <!-- userSpaceOnUse: a selected (thicker) line must NOT inflate its arrowhead. -->
          <marker id="arr-seq" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient="auto"><path d="M0 0 L12 6 L0 12 Z"></path></marker>
          <marker id="arr-success" class="m-success" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient="auto"><path d="M0 0 L12 6 L0 12 Z"></path></marker>
          <marker id="arr-failure" class="m-failure" markerUnits="userSpaceOnUse" markerWidth="12" markerHeight="12" refX="11" refY="6" orient="auto"><path d="M0 0 L12 6 L0 12 Z"></path></marker>
        </defs>
        <g class="world">
          <g class="edges"></g>
          <g class="nodes"></g>
          <g class="extras"></g>
          <path class="rubber" hidden d=""></path>
        </g>
      </svg>
    `;
    this._bindPointer();
  }

  /** @private */
  renderGraph() {
    const g = this._graph;
    const nodesG = this.$('.nodes');
    const edgesG = this.$('.edges');
    const extrasG = this.$('.extras');
    if (!nodesG) return;
    nodesG.replaceChildren();
    edgesG.replaceChildren();
    extrasG.replaceChildren();
    if (!g || !g.nodes.length) return;

    // Parallel edges (same from→to, e.g. two failure gotos with different criteria) fan out
    // side-by-side instead of drawing on top of one another.
    const groups = new Map();
    for (const edge of g.edges) {
      const key = `${edge.from}|${edge.to}`;
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(edge.id);
    }
    this._parallel = new Map();
    for (const ids of groups.values()) {
      ids.forEach((id, i) => this._parallel.set(id, { i, n: ids.length }));
    }
    this._pseudoIds = new Set(g.nodes.filter((n) => n.pseudo).map((n) => n.id));

    // Arrivals spread along the target border so arrowheads never stack: every edge landing on a
    // node gets its own landing point, ordered by where it comes from.
    this._inbound = new Map();
    const inboundGroups = new Map();
    for (const edge of g.edges) {
      if (edge.to.startsWith('workflow:')) continue;
      if (!inboundGroups.has(edge.to)) inboundGroups.set(edge.to, []);
      inboundGroups.get(edge.to).push(edge);
    }
    for (const group of inboundGroups.values()) {
      const sorted = [...group].sort((p, q) =>
        ((this._positions[p.from]?.x ?? 0) - (this._positions[q.from]?.x ?? 0)) || (p.id < q.id ? -1 : 1));
      sorted.forEach((edge, i) => this._inbound.set(edge.id, (i - (sorted.length - 1) / 2) * 16));
    }

    // Exit chips (edges targeting `workflow:<id>`) get synthetic positions next to their source.
    this._exitPositions = {};
    for (const edge of g.edges) {
      if (!edge.to.startsWith('workflow:')) continue;
      const from = this._positions[edge.from];
      if (from && !this._exitPositions[edge.to]) {
        this._exitPositions[edge.to] = { x: from.x + NODE_WIDTH + 56, y: from.y + NODE_HEIGHT / 2 - 14 };
      }
    }

    for (const edge of g.edges) edgesG.append(this._buildEdge(edge));
    for (const node of g.nodes) nodesG.append(this._buildNode(node));
    for (const [id, pos] of Object.entries(this._exitPositions)) extrasG.append(this._buildExitChip(id, pos));
    if (g.defaults.successActions.length || g.defaults.failureActions.length) {
      extrasG.append(this._buildDefaultsCard(g.defaults));
    }

    this._applyView();
    this._applySelection();
    this._applyBreakpoints();
    this._applyDebug();
    if (!this._fitted) { this._fitted = true; requestAnimationFrame(() => this.fit()); }
  }

  /** @private */
  _buildNode(node) {
    const pos = this._positions[node.id];
    const el = document.createElementNS(SVG, 'g');
    el.setAttribute('class', `node${node.pseudo ? ' pseudo' : ''} kind-${node.kind}`);
    el.setAttribute('data-id', node.id);
    el.setAttribute('transform', `translate(${pos.x} ${pos.y})`);

    if (node.pseudo) {
      const cx = NODE_WIDTH / 2;
      const cy = NODE_HEIGHT / 2;
      const badge = node.kind === 'start'
        ? (node.inputCount ? `⇥ ${node.inputCount} input${node.inputCount === 1 ? '' : 's'}` : '')
        : (node.outputCount ? `→ ${node.outputCount} output${node.outputCount === 1 ? '' : 's'}` : '');
      el.innerHTML = `
        <circle class="card" cx="${cx}" cy="${cy}" r="${PSEUDO_R}"></circle>
        ${node.kind === 'start'
          ? `<text class="glyph" x="${cx}" y="${cy + 3.5}" text-anchor="middle">▶</text>
        <circle class="port port-entry" cx="${cx}" cy="${cy + PSEUDO_R}" r="5"><title>Drag onto a step to make it the workflow's FIRST step</title></circle>`
          : `<circle class="inner" cx="${cx}" cy="${cy}" r="6"></circle>`}
        <text class="plabel" x="${cx}" y="${cy + PSEUDO_R + 14}" text-anchor="middle">${escapeHtml(node.label)}</text>
        ${badge ? `<text class="chip" x="${cx}" y="${cy + PSEUDO_R + 28}" text-anchor="middle">${escapeHtml(badge)}</text>` : ''}
      `;
      return el;
    }

    const chips = [];
    if (node.criteriaCount) chips.push({ text: `✓ ${node.criteriaCount}` });
    if (node.outputCount) chips.push({ text: `→ ${node.outputCount}` });
    if (node.retry) chips.push({ text: `↻ ×${node.retry.retryLimit ?? '∞'}${node.retry.retryAfter != null ? ` / ${node.retry.retryAfter}s` : ''}` });
    if (node.usesDefaultFailure || node.usesDefaultSuccess) chips.push({ text: '⌁ defaults', cls: ' ghost' });
    const title = node.description ? `<title>${escapeHtml(node.description)}</title>` : '';

    el.innerHTML = `
      ${title}
      <rect class="card" width="${NODE_WIDTH}" height="${NODE_HEIGHT}"></rect>
      <circle class="bp" cx="0" cy="18" r="5"><title>Toggle breakpoint</title></circle>
      <rect class="kbadge" x="12" y="10" width="26" height="14"></rect>
      <text class="kbadge-text" x="25" y="20.5" text-anchor="middle">${KIND_BADGE[node.kind]}</text>
      <text class="label" x="46" y="22">${escapeHtml(truncate(node.label, 20))}</text>
      <text class="sub" x="12" y="42">${escapeHtml(truncate(node.sublabel, 30))}</text>
      <text class="chips" x="12" y="62">${chips.map((c) => `<tspan class="chip${c.cls || ''}" dx="0 6">${escapeHtml(c.text)}</tspan>`).join('<tspan dx="8"> </tspan>')}</text>
      <circle class="port port-failure" cx="${NODE_WIDTH * 0.35}" cy="${NODE_HEIGHT}" r="5"><title>Draw a failure edge</title></circle>
      <circle class="port port-success" cx="${NODE_WIDTH * 0.65}" cy="${NODE_HEIGHT}" r="5"><title>Draw a success edge</title></circle>
    `;
    return el;
  }

  /** @private */
  _buildEdge(edge) {
    const el = document.createElementNS(SVG, 'g');
    el.setAttribute('class', `edge edge-${edge.kind}${edge.reusable ? ' reusable' : ''}${edge.unreachable ? ' unreachable' : ''}`);
    el.setAttribute('data-id', edge.id);
    const d = this._edgePath(edge);
    const mid = this._edgeMid(edge);
    // An explicit action edge with no criteria fires unconditionally — say so, visibly and
    // clickably, instead of leaving the missing conditions silent. Dispatch is first-match-wins,
    // so when a step has several same-kind actions the label leads with its precedence.
    let label = edge.criteriaSummary ?? (edge.kind !== 'seq' ? 'always' : undefined);
    if (label && edge.orderCount > 1) label = `${edge.order}· ${label}`;
    el.innerHTML = `
      <path class="hit" d="${d}"></path>
      <path class="line" d="${d}" marker-end="url(#arr-${edge.kind === 'seq' ? 'seq' : edge.kind})"></path>
      ${label ? `<text class="elabel${edge.criteriaSummary ? '' : ' ghost'}" x="${mid.x + 8}" y="${mid.y + ((edge.order ?? 1) - 1) * 12}">${escapeHtml(truncate(label, 34))}</text>` : ''}
    `;
    return el;
  }

  /** @private */
  _buildExitChip(id, pos) {
    const label = id.slice('workflow:'.length);
    const el = document.createElementNS(SVG, 'g');
    el.setAttribute('class', 'exit-chip');
    el.setAttribute('data-id', id);
    el.setAttribute('transform', `translate(${pos.x} ${pos.y})`);
    const w = Math.min(11 * label.length + 34, 220);
    el.innerHTML = `
      <rect width="${w}" height="26"></rect>
      <text x="10" y="17">⇥ ${escapeHtml(truncate(label, 24))}</text>
    `;
    return el;
  }

  /** @private */
  _buildDefaultsCard(defaults) {
    const rows = [
      ...defaults.successActions.map((a) => ({ ...a, dir: 'success' })),
      ...defaults.failureActions.map((a) => ({ ...a, dir: 'failure' })),
    ];
    const xs = Object.values(this._positions).map((p) => p.x);
    const x = (xs.length ? Math.max(...xs) : 0) + NODE_WIDTH + 72;
    const el = document.createElementNS(SVG, 'g');
    el.setAttribute('class', 'defaults');
    el.setAttribute('data-id', 'defaults');
    el.setAttribute('transform', `translate(${x} 0)`);
    const w = 190;
    const h = 30 + rows.length * 18;
    el.innerHTML = `
      <title>Workflow-level default actions — what a step falls back to when it declares no onSuccess/onFailure of its own. Click to edit.</title>
      <rect class="card" width="${w}" height="${h}"></rect>
      <text class="title" x="12" y="19">WORKFLOW DEFAULTS</text>
      ${rows.map((r, i) => `<text class="row" x="12" y="${37 + i * 18}">${r.dir === 'failure' ? '✗' : '✓'} ${escapeHtml(truncate(`${r.name || r.type} (${r.type})`, 24))}</text>`).join('')}
    `;
    return el;
  }

  /** @private — the parallel-fan offset for an edge: 0 when it has the route to itself. */
  _fan(edge) {
    const p = this._parallel?.get(edge.id);
    return p ? (p.i - (p.n - 1) / 2) * 30 : 0;
  }

  /** @private — where edges attach on a node: card borders, or the pseudo-node circle. */
  _anchors(id) {
    const pos = this._positions[id];
    if (this._pseudoIds?.has(id)) {
      const cx = pos.x + NODE_WIDTH / 2;
      const cy = pos.y + NODE_HEIGHT / 2;
      return {
        top: { x: cx, y: cy - PSEUDO_R },
        bottom: { x: cx, y: cy + PSEUDO_R },
        right: { x: cx + PSEUDO_R, y: cy },
      };
    }
    return {
      top: { x: pos.x + NODE_WIDTH / 2, y: pos.y },
      bottom: { x: pos.x + NODE_WIDTH / 2, y: pos.y + NODE_HEIGHT },
      right: { x: pos.x + NODE_WIDTH, y: pos.y + NODE_HEIGHT / 2 },
    };
  }

  /** @private — smooth cubic between node borders; backward/sideways edges bow out to the side. */
  _edgePath(edge) {
    const a = this._positions[edge.from];
    const b = this._positions[edge.to] || this._exitPositions?.[edge.to];
    if (!a || !b) return '';
    const fan = this._fan(edge);
    if (this._exitPositions?.[edge.to]) {
      const x1 = a.x + NODE_WIDTH;
      const y1 = a.y + NODE_HEIGHT / 2;
      return `M ${x1} ${y1} C ${x1 + 28} ${y1}, ${b.x - 28} ${b.y + 13}, ${b.x} ${b.y + 13}`;
    }
    const from = this._anchors(edge.from);
    const to = this._anchors(edge.to);
    const down = b.y > a.y + NODE_HEIGHT / 2;
    const arrive = this._inbound?.get(edge.id) ?? 0;
    if (down) {
      const x1 = from.bottom.x + fan;
      const y1 = from.bottom.y;
      const x2 = to.top.x + fan + arrive;
      const y2 = to.top.y;
      const c = Math.max(24, (y2 - y1) / 2);
      return `M ${x1} ${y1} C ${x1 + fan} ${y1 + c}, ${x2 + fan + arrive} ${y2 - c}, ${x2} ${y2}`;
    }
    // Upward or same-rank: route around the right side, arrivals spread down the border.
    const x1 = from.right.x;
    const y1 = from.right.y;
    const x2 = to.right.x;
    const y2 = to.right.y + arrive * 0.6;
    const bow = 70 + Math.abs(y2 - y1) * 0.08 + Math.abs(fan);
    return `M ${x1} ${y1} C ${x1 + bow} ${y1}, ${x2 + bow} ${y2}, ${x2} ${y2}`;
  }

  /** @private */
  _edgeMid(edge) {
    const a = this._positions[edge.from];
    const b = this._positions[edge.to] || this._exitPositions?.[edge.to];
    if (!a || !b) return { x: 0, y: 0 };
    const fan = this._fan(edge);
    return { x: (a.x + b.x + NODE_WIDTH) / 2 + fan, y: (a.y + NODE_HEIGHT + b.y) / 2 + fan * 0.5 };
  }

  // ── Class application (no rebuilds) ───────────────────────────────────────────────────────────

  /** @private */
  _applyView() {
    const world = this.$('.world');
    if (world) world.setAttribute('transform', `translate(${this._view.tx} ${this._view.ty}) scale(${this._view.k})`);
  }

  /** @private */
  _applySelection() {
    for (const el of this.$$('.node, .edge, .defaults')) {
      const sel = this._selection;
      const id = el.getAttribute('data-id');
      const type = el.classList.contains('node') ? 'node' : el.classList.contains('edge') ? 'edge' : 'defaults';
      el.classList.toggle('selected', !!sel && sel.type === type && (type === 'defaults' || sel.id === id));
    }

    // A selected ACTION edge grows a drag handle at its arrowhead: drag it onto another node to
    // retarget the action (sequence edges are the step order — they have no handle).
    this.$$('.retarget').forEach((el) => el.remove());
    const sel = this._selection;
    if (sel?.type === 'edge' && !this.readonly) {
      const edge = this._graph?.edges.find((x) => x.id === sel.id);
      if (edge && edge.kind !== 'seq' && edge.actionName) {
        const end = this._edgeEnd(edge);
        if (end) {
          const handle = document.createElementNS(SVG, 'circle');
          handle.setAttribute('class', 'retarget');
          handle.setAttribute('data-id', edge.id);
          handle.setAttribute('r', '7');
          handle.setAttribute('cx', end.x);
          handle.setAttribute('cy', end.y);
          handle.append(Object.assign(document.createElementNS(SVG, 'title'), { textContent: 'Drag onto another step (or the end terminal) to retarget this action' }));
          this.$('.extras').append(handle);
        }
      }
    }
  }

  /** @private — where an edge's arrow lands (mirrors _edgePath's end arithmetic). */
  _edgeEnd(edge) {
    const a = this._positions[edge.from];
    const b = this._positions[edge.to] || this._exitPositions?.[edge.to];
    if (!a || !b) return null;
    if (this._exitPositions?.[edge.to]) return { x: b.x, y: b.y + 13 };
    const fan = this._fan(edge);
    const arrive = this._inbound?.get(edge.id) ?? 0;
    const to = this._anchors(edge.to);
    const down = b.y > a.y + NODE_HEIGHT / 2;
    return down
      ? { x: to.top.x + fan + arrive, y: to.top.y }
      : { x: to.right.x, y: to.right.y + arrive * 0.6 };
  }

  /** @private */
  _applyBreakpoints() {
    for (const el of this.$$('.node')) {
      el.classList.toggle('bp-on', this._breakpoints.has(el.getAttribute('data-id')));
    }
  }

  /** @private */
  _applyDebug() {
    const svg = this.$('svg');
    if (!svg) return;
    const state = this._debugState;
    svg.classList.toggle('debugging', !!state);
    for (const el of this.$$('.node')) {
      const id = el.getAttribute('data-id');
      el.classList.toggle('st-active', !!state && state.active === id);
      const status = state?.steps?.[id];
      el.classList.toggle('st-done-success', status === 'done-success');
      el.classList.toggle('st-done-failure', status === 'done-failure');
      el.classList.toggle('st-skipped', status === 'skipped');
    }
    const lit = new Set(state?.edges || []);
    for (const el of this.$$('.edge')) {
      el.classList.toggle('lit', lit.has(el.getAttribute('data-id')));
    }
  }

  // ── Interaction (pointer state machine; shadow-root listeners only) ──────────────────────────

  /** @private */
  _bindPointer() {
    const svg = this.$('svg');

    // Operation drops from the browser rail (§3.2): the payload rides HTML5 DnD under our own
    // MIME type; the drop point maps through the view transform so the step lands where released.
    svg.addEventListener('dragover', (e) => {
      if (this.readonly) return;
      if ([...e.dataTransfer.types].includes('application/x-arazzo-operation')) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
      }
    });
    svg.addEventListener('drop', (e) => {
      if (this.readonly) return;
      const raw = e.dataTransfer.getData('application/x-arazzo-operation');
      if (!raw) return;
      e.preventDefault();
      let payload;
      try { payload = JSON.parse(raw); } catch { return; }
      if (!payload?.operation) return;
      this.emit('operation-dropped', { ...payload, position: this._toWorld(e) });
    });

    svg.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      const hit = this._hitOf(e);
      try {
        svg.setPointerCapture(e.pointerId);
      } catch {
        // Synthetic or already-released pointers (tests, exotic input) have no capturable id;
        // the gesture still works — capture only guards against leaving the element mid-drag.
      }
      if (hit?.port && !this.readonly) {
        this._gesture = { type: 'draw', from: hit.id, kind: hit.port, moved: false };
        const from = this._positions[hit.id];
        const origin = hit.port === 'entry'
          ? { x: from.x + NODE_WIDTH / 2, y: from.y + NODE_HEIGHT / 2 + PSEUDO_R }
          : { x: from.x + NODE_WIDTH * (hit.port === 'success' ? 0.65 : 0.35), y: from.y + NODE_HEIGHT };
        this._gesture.origin = origin;
        const rubber = this.$('.rubber');
        rubber.setAttribute('d', `M ${origin.x} ${origin.y} L ${origin.x} ${origin.y}`);
        rubber.classList.toggle('rubber-success', hit.port === 'success');
        rubber.classList.toggle('rubber-failure', hit.port === 'failure');
        rubber.classList.toggle('rubber-entry', hit.port === 'entry');
        // NB: SVG elements have no `hidden` IDL property — toggle the attribute, never the property.
        rubber.removeAttribute('hidden');
      } else if (hit?.retarget && !this.readonly) {
        const edge = this._graph?.edges.find((x) => x.id === hit.id);
        const from = this._positions[edge?.from];
        if (edge && from) {
          this._gesture = { type: 'retarget', edge, origin: { x: from.x + NODE_WIDTH / 2, y: from.y + NODE_HEIGHT }, moved: false };
          const rubber = this.$('.rubber');
          rubber.setAttribute('d', `M ${this._gesture.origin.x} ${this._gesture.origin.y} L ${this._gesture.origin.x} ${this._gesture.origin.y}`);
          rubber.classList.toggle('rubber-success', edge.kind === 'success');
          rubber.classList.toggle('rubber-failure', edge.kind === 'failure');
          rubber.classList.remove('rubber-entry');
          rubber.removeAttribute('hidden');
        }
      } else if (hit?.bp) {
        this._gesture = { type: 'bp', id: hit.id };
      } else if (hit?.type === 'node') {
        const pos = this._positions[hit.id];
        const p = this._toWorld(e);
        this._gesture = { type: 'drag', id: hit.id, dx: p.x - pos.x, dy: p.y - pos.y, moved: false };
      } else if (hit) {
        this._gesture = { type: 'click', hit };
      } else {
        this._gesture = { type: 'pan', x: e.clientX, y: e.clientY, tx: this._view.tx, ty: this._view.ty, moved: false };
        svg.classList.add('panning');
      }
    });

    svg.addEventListener('pointermove', (e) => {
      const g = this._gesture;
      if (!g) return;
      if (g.type === 'pan') {
        g.moved = true;
        this._view.tx = g.tx + (e.clientX - g.x);
        this._view.ty = g.ty + (e.clientY - g.y);
        this._applyView();
      } else if (g.type === 'drag') {
        if (this.readonly) return;
        g.moved = true;
        const p = this._toWorld(e);
        this._overrides[g.id] = { x: Math.round(p.x - g.dx), y: Math.round(p.y - g.dy) };
        this._positions[g.id] = this._overrides[g.id];
        this._moveNode(g.id);
      } else if (g.type === 'draw' || g.type === 'retarget') {
        g.moved = true;
        const p = this._toWorld(e);
        const rubber = this.$('.rubber');
        rubber.setAttribute('d', `M ${g.origin.x} ${g.origin.y} L ${p.x} ${p.y}`);
        const over = this._nodeAt(p, g.type === 'retarget' ? g.edge.from : g.from);
        for (const el of this.$$('.node')) el.classList.toggle('link-target', over === el.getAttribute('data-id'));
        g.over = over;
      }
    });

    svg.addEventListener('pointerup', (e) => {
      const g = this._gesture;
      this._gesture = null;
      svg.classList.remove('panning');
      if (!g) return;
      if (g.type === 'pan' && !g.moved) {
        this._select(null);
      } else if (g.type === 'drag') {
        if (g.moved) this.emit('layout-changed', { overrides: this.layoutOverrides });
        else this._select({ type: 'node', id: g.id });
      } else if (g.type === 'bp') {
        const enabled = !this._breakpoints.has(g.id);
        this._breakpoints[enabled ? 'add' : 'delete'](g.id);
        this._applyBreakpoints();
        this.emit('breakpoint-toggled', { stepId: g.id, enabled });
      } else if (g.type === 'click') {
        this._select(g.hit.type === 'exit' ? { type: 'edge', id: g.hit.id } : { type: g.hit.type, id: g.hit.id });
      } else if (g.type === 'draw') {
        this.$('.rubber').setAttribute('hidden', '');
        for (const el of this.$$('.node.link-target')) el.classList.remove('link-target');
        // The start node is never a valid action target; dropping on the end terminal authors
        // an `end` action (the host writes it — one grammar, every action is an edge). An ENTRY
        // drag (from the start port) must land on a real step: it reorders that step to the front
        // of the steps array, so pseudo targets mean nothing.
        if (g.kind === 'entry') {
          if (g.over && g.over !== g.from && !this._pseudoIds.has(g.over)) {
            this.emit('entry-changed', { stepId: g.over });
          }
        } else if (g.over && g.over !== g.from && g.over !== START_ID) {
          this.emit('edge-created', { from: g.from, to: g.over, kind: g.kind });
        }
      } else if (g.type === 'retarget') {
        this.$('.rubber').setAttribute('hidden', '');
        for (const el of this.$$('.node.link-target')) el.classList.remove('link-target');
        if (g.over && g.over !== g.edge.from && g.over !== START_ID && g.over !== g.edge.to) {
          this.emit('edge-retargeted', { id: g.edge.id, actionName: g.edge.actionName, from: g.edge.from, kind: g.edge.kind, to: g.over });
        }
      }
    });

    svg.addEventListener('pointercancel', () => {
      this._gesture = null;
      svg.classList.remove('panning');
      this.$('.rubber').setAttribute('hidden', '');
    });

    svg.addEventListener('wheel', (e) => {
      e.preventDefault();
      const factor = Math.exp(-e.deltaY * 0.0012);
      const k = Math.min(2.5, Math.max(0.2, this._view.k * factor));
      const box = svg.getBoundingClientRect();
      const cx = e.clientX - box.left;
      const cy = e.clientY - box.top;
      // Keep the point under the cursor stationary while scaling.
      this._view.tx = cx - ((cx - this._view.tx) / this._view.k) * k;
      this._view.ty = cy - ((cy - this._view.ty) / this._view.k) * k;
      this._view.k = k;
      this._applyView();
    }, { passive: false });

    svg.addEventListener('dblclick', (e) => {
      const hit = this._hitOf(e);
      if (hit?.type === 'node') this.emit('node-activated', { stepId: hit.id });
    });

    svg.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        this._gesture = null;
        this.$('.rubber').setAttribute('hidden', '');
        this._select(null);
      } else if ((e.key === 'Delete' || e.key === 'Backspace') && this._selection && !this.readonly) {
        e.preventDefault();
        this.emit('delete-requested', { selection: this._selection });
      } else if (e.key.startsWith('Arrow') && this._selection?.type === 'node' && !this.readonly) {
        e.preventDefault();
        const d = { ArrowUp: [0, -12], ArrowDown: [0, 12], ArrowLeft: [-12, 0], ArrowRight: [12, 0] }[e.key];
        const pos = this._positions[this._selection.id];
        this._overrides[this._selection.id] = { x: pos.x + d[0], y: pos.y + d[1] };
        this._positions[this._selection.id] = this._overrides[this._selection.id];
        this._moveNode(this._selection.id);
        this.emit('layout-changed', { overrides: this.layoutOverrides });
      }
    });
  }

  /** @private — classify what a pointer event landed on, using the composed path (never the document). */
  _hitOf(e) {
    for (const el of e.composedPath()) {
      if (!(el instanceof Element)) break;
      if (el.classList?.contains('port')) {
        const kind = [...el.classList].map((c) => PORT_KINDS[c]).find(Boolean);
        return { port: kind, id: el.closest('.node').getAttribute('data-id') };
      }
      if (el.classList?.contains('bp')) return { bp: true, id: el.closest('.node').getAttribute('data-id') };
      if (el.classList?.contains('node')) return { type: 'node', id: el.getAttribute('data-id') };
      if (el.classList?.contains('retarget')) return { retarget: true, id: el.getAttribute('data-id') };
      if (el.classList?.contains('edge')) return { type: 'edge', id: el.getAttribute('data-id') };
      if (el.classList?.contains('defaults')) return { type: 'defaults', id: 'defaults' };
      if (el.classList?.contains('exit-chip')) return { type: 'exit', id: el.getAttribute('data-id') };
      if (el === this.$('svg')) break;
    }
    return null;
  }

  /** @private — pointer event → world coordinates via our own view transform. */
  _toWorld(e) {
    const box = this.$('svg').getBoundingClientRect();
    return {
      x: (e.clientX - box.left - this._view.tx) / this._view.k,
      y: (e.clientY - box.top - this._view.ty) / this._view.k,
    };
  }

  /** @private — geometric node hit-test in world coordinates (the elementFromPoint replacement). */
  _nodeAt(p, excludeId) {
    for (const node of this._graph?.nodes || []) {
      if (node.id === excludeId) continue;
      const pos = this._positions[node.id];
      if (p.x >= pos.x && p.x <= pos.x + NODE_WIDTH && p.y >= pos.y && p.y <= pos.y + NODE_HEIGHT) {
        return node.id;
      }
    }
    return null;
  }

  /** @private */
  _moveNode(id) {
    const pos = this._positions[id];
    this.$(`.node[data-id="${cssEscape(id)}"]`)?.setAttribute('transform', `translate(${pos.x} ${pos.y})`);
    // Re-path the edges touching the node (cheap: a handful per node).
    for (const edge of this._graph?.edges || []) {
      if (edge.from !== id && edge.to !== id) continue;
      // querySelectorAll: ids are unique by construction, but stale twins must never survive a move.
      for (const el of this.$$(`.edge[data-id="${cssEscape(edge.id)}"]`)) {
        const d = this._edgePath(edge);
        el.querySelector('.hit').setAttribute('d', d);
        el.querySelector('.line').setAttribute('d', d);
        const label = el.querySelector('.elabel');
        if (label) {
          const mid = this._edgeMid(edge);
          label.setAttribute('x', mid.x + 8);
          // Keep the parallel-edge label stacking offset _buildEdge applies (line ~394), else
          // labels on parallel edges collapse onto one line after a drag.
          label.setAttribute('y', mid.y + ((edge.order ?? 1) - 1) * 12);
        }
      }
    }
  }

  /** @private */
  _select(selection) {
    this._selection = selection;
    this._applySelection();
    this.emit('selection-changed', { selection });
  }
}

function truncate(text, max) {
  const s = String(text ?? '');
  return s.length > max ? `${s.slice(0, max - 1)}…` : s;
}

function cssEscape(value) {
  return (globalThis.CSS?.escape ?? ((v) => String(v).replaceAll(/([^\w-])/g, '\\$1')))(value);
}

define('arazzo-design-surface', ArazzoDesignSurface);
export { ArazzoDesignSurface };
