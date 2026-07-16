// workflow-diff.js — the workflow comparison model (Layer 0.5, DOM-free; sibling of workflow-graph.js
// and workflow-layout.js). It classifies a workflow pair's steps, edges, and workflow-level surfaces
// as added / removed / changed / renamed / moved ON TOP OF the projection (workflow-graph.js) and the
// in-house identity-aware structural diff (workflow-document-model.js) — it never writes a second
// structural differ. Nothing here knows SVG exists (design workflow-designer-design.md §6.4 / the
// visual-diff design §4). No server or OpenAPI knowledge.
//
//   import { diffWorkflowPair } from './workflow-diff.js';
//   const r = diffWorkflowPair(leftDoc, rightDoc, { leftWorkflowId, rightWorkflowId });
//   // r.leftGraph / r.rightGraph — the projectWorkflow outputs (reuse; do not re-project)
//   // r.idMap  — Map<leftStepId, rightStepId> (identity matches + renames; #start/#end included)
//   // r.steps / r.flow / r.workflow — change-list entries (structured; a host formats them, §4.5)
//   // r.summary — { added, removed, changed, renamed, moved }
//   // r.paint.{left,right} — diffState-ready { nodes, edges, notes } keyed by each side's projection ids (§5)
//   // r.layout.{left,right} — shared union-layout positions per side, so matched steps sit level (§4.6)
//
//   import { buildGhostProjection } from './workflow-diff.js';
//   const g = buildGhostProjection(r, 'right'); // overlay mode's data (§4.7): ONE union graph in the
//   // base side's id-space (base solid, the other side's exclusives appended as translucent ghosts).
//   // g.graph / g.paint (diffState with ghosts + overlay:true) / g.layout / g.entryMap (projection id → surface id).
//
// Equality is JSON.stringify (key-order-SENSITIVE), consistent with the document model's deepEqual.
// Whole-value comparisons here (workflow inputs/outputs/defaults/components and the rename-group
// similarity score) therefore flag a key-order-only difference as changed; step CONTENT flows through
// the model's recursive `diff`, which compares per key and so does not. The only compare sources are
// same-lineage documents (a working copy vs a catalog version of the same workflow), whose key order is
// stable, so this is an accepted, pinned limitation (see the key-order test).
import { projectWorkflow, START_ID, END_ID } from './workflow-graph.js';
import { diff } from './workflow-document-model.js';
import { layoutGraph } from './workflow-layout.js';

const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);

const findWorkflow = (doc, id) => (doc?.workflows || []).find((w) => w.workflowId === id);

// A step's semantic anchor — the rename gate (§4.2). A renamed step keeps its binding; two steps that
// share no binding never pair. The channel key includes the action, so a send never pairs a receive.
function bindingKey(step) {
  if (step.operationId != null) return `op:${step.operationId}`;
  if (step.operationPath != null) return `oppath:${step.operationPath}`;
  if (step.channelPath != null) return `ch:${step.channelPath}:${step.action ?? 'send'}`;
  if (step.workflowId != null) return `wf:${step.workflowId}`;
  return null; // no binding — never pairs
}

// The field groups whose deep-equality drives the rename similarity score (§4.2).
const RENAME_GROUPS = ['parameters', 'requestBody', 'successCriteria', 'onSuccess', 'onFailure', 'outputs', 'description'];

// Maps each RESOLVED action edge (by 1-based order) to its RAW list index + reusable component name.
// The projection resolves onSuccess/onFailure, dereferencing $components refs and dropping unresolvable
// ones, so a resolved index is NOT a raw index — merge payloads must address the raw document (§4.3.6).
function rawActionMap(doc, rawList) {
  const map = [];
  for (let i = 0; i < (rawList || []).length; i++) {
    const entry = rawList[i];
    if (entry && typeof entry.reference === 'string') {
      const m = /^\$components\.(successActions|failureActions)\.([^.]+)$/.exec(entry.reference);
      if (m && doc?.components?.[m[1]]?.[m[2]]) map.push({ rawIndex: i, componentName: m[2], reference: entry.reference });
      // an unresolvable reference is dropped from the resolved list, so it contributes no edge/map slot
    } else if (entry) {
      map.push({ rawIndex: i });
    }
  }
  return map;
}

// The $components action bodies a workflow's steps reference (resolvable only), as
// `${list}.${name}` → the raw reference string, so a shared-body change can be attributed (§4.4).
function referencedComponents(doc, workflow) {
  const refs = new Map();
  for (const step of workflow?.steps || []) {
    for (const list of [step.onSuccess, step.onFailure]) {
      for (const entry of list || []) {
        if (entry && typeof entry.reference === 'string') {
          const m = /^\$components\.(successActions|failureActions)\.([^.]+)$/.exec(entry.reference);
          if (m && doc?.components?.[m[1]]?.[m[2]]) refs.set(`${m[1]}.${m[2]}`, entry.reference);
        }
      }
    }
  }
  return refs;
}

export function diffWorkflowPair(leftDoc, rightDoc, { leftWorkflowId, rightWorkflowId } = {}) {
  const leftGraph = projectWorkflow(leftDoc, leftWorkflowId);
  const rightGraph = projectWorkflow(rightDoc, rightWorkflowId);
  const leftWf = findWorkflow(leftDoc, leftWorkflowId);
  const rightWf = findWorkflow(rightDoc, rightWorkflowId);
  const leftSteps = leftWf?.steps || [];
  const rightSteps = rightWf?.steps || [];
  const leftIndex = new Map(leftSteps.map((s, i) => [s.stepId, i]));
  const rightIndex = new Map(rightSteps.map((s, i) => [s.stepId, i]));

  // ── §4.2 step matching: identity, then binding-gated rename over the residue ──────────────────────
  const idMap = new Map([[START_ID, START_ID], [END_ID, END_ID]]);
  const rightById = new Set(rightSteps.map((s) => s.stepId));
  for (const s of leftSteps) if (rightById.has(s.stepId)) idMap.set(s.stepId, s.stepId);

  const matchedRight = new Set(idMap.values());
  const leftResidue = leftSteps.filter((s) => !idMap.has(s.stepId));
  const rightResidue = rightSteps.filter((s) => !matchedRight.has(s.stepId));
  const candidates = [];
  for (const L of leftResidue) {
    const lk = bindingKey(L);
    if (lk == null) continue;
    for (const R of rightResidue) {
      if (bindingKey(R) !== lk) continue;
      let equalGroups = 0;
      for (const g of RENAME_GROUPS) if (eq(L[g], R[g])) equalGroups++;
      candidates.push({ L, R, li: leftIndex.get(L.stepId), ri: rightIndex.get(R.stepId), sim: equalGroups / RENAME_GROUPS.length });
    }
  }
  // greedy, deterministic: (similarity desc, |li−ri| asc, li asc, rightId asc)
  candidates.sort((a, b) =>
    (b.sim - a.sim)
    || (Math.abs(a.li - a.ri) - Math.abs(b.li - b.ri))
    || (a.li - b.li)
    || (a.R.stepId < b.R.stepId ? -1 : a.R.stepId > b.R.stepId ? 1 : 0));
  const renamedLeftToRight = new Map();
  const usedLeft = new Set();
  const usedRight = new Set();
  for (const c of candidates) {
    if (usedLeft.has(c.L.stepId) || usedRight.has(c.R.stepId)) continue;
    usedLeft.add(c.L.stepId);
    usedRight.add(c.R.stepId);
    idMap.set(c.L.stepId, c.R.stepId);
    renamedLeftToRight.set(c.L.stepId, c.R.stepId);
  }
  const renamedRight = new Set(renamedLeftToRight.values());

  // ── §4.2 content classification via the rename-normalized identity-list diff ──────────────────────
  // Rewrite each renamed left step's id to its right id, then diff — identity matching + minimal moves
  // come free, and the ops address steps by identity. Read them; never re-derive moves from indices.
  const normalizedLeft = leftSteps.map((s) => {
    const to = idMap.get(s.stepId);
    return to && to !== s.stepId ? { ...s, stepId: to } : s;
  });
  const ops = diff({ steps: normalizedLeft }, { steps: rightSteps });
  const removedLeftIds = new Set();
  const addedRightIds = new Set();
  const movedRightIds = new Set();
  const changedGroups = new Map(); // right stepId → Set(group)
  for (const op of ops) {
    if (op.path[0] !== 'steps') continue;
    const seg = op.path[1];
    if (op.kind === 'remove' && op.path.length === 2 && seg && typeof seg === 'object') {
      removedLeftIds.add(seg.id); // a removed step is left-only (renamed ones were remapped): id === its left id
    } else if (op.kind === 'insert' && op.path.length === 1 && op.value) {
      addedRightIds.add(op.value.stepId);
    } else if (op.kind === 'move' && op.path.length === 1) {
      movedRightIds.add(rightSteps[op.to]?.stepId);
    } else if (op.path.length > 2 && seg && typeof seg === 'object') {
      const group = typeof op.path[2] === 'object' ? op.path[2].id : op.path[2];
      if (!changedGroups.has(seg.id)) changedGroups.set(seg.id, new Set());
      changedGroups.get(seg.id).add(group);
    }
  }

  // ── build step paint maps + change-list entries (§4.2 table, §4.5 order) ──────────────────────────
  const paint = { left: { nodes: {}, edges: {}, notes: {} }, right: { nodes: {}, edges: {}, notes: {} } };
  const summary = { added: 0, removed: 0, changed: 0, renamed: 0, moved: 0 };
  const stepEntries = [];

  // left declaration order first: removed / renamed / changed / moved (matched steps emit from the left)
  for (const s of leftSteps) {
    const leftId = s.stepId;
    if (removedLeftIds.has(leftId)) {
      paint.left.nodes[leftId] = 'removed';
      summary.removed++;
      stepEntries.push({ group: 'steps', type: 'removed', id: leftId, leftId });
      continue;
    }
    const rightId = idMap.get(leftId);
    const groups = changedGroups.has(rightId) ? [...changedGroups.get(rightId)].sort() : [];
    if (renamedLeftToRight.has(leftId)) {
      paint.left.nodes[leftId] = 'changed';
      paint.right.nodes[rightId] = 'changed';
      paint.left.notes[leftId] = `now ${rightId}`;
      paint.right.notes[rightId] = `was ${leftId}`;
      summary.renamed++; // a renamed step counts as renamed only; its content deltas ride the same entry
      stepEntries.push({ group: 'steps', type: 'renamed', id: rightId, leftId, rightId, changedGroups: groups });
    } else if (groups.length) {
      paint.left.nodes[leftId] = 'changed';
      paint.right.nodes[rightId] = 'changed';
      summary.changed++;
      stepEntries.push({ group: 'steps', type: 'changed', id: rightId, leftId, rightId, changedGroups: groups });
    } else if (movedRightIds.has(rightId)) {
      // moved: content identical, only position differs — never painted; the seq edges + this entry carry it
      summary.moved++;
      stepEntries.push({ group: 'steps', type: 'moved', id: rightId, leftId, rightId, fromIndex: leftIndex.get(leftId), toIndex: rightIndex.get(rightId) });
    }
  }
  // then right declaration order: added (right-only)
  for (const s of rightSteps) {
    if (addedRightIds.has(s.stepId)) {
      paint.right.nodes[s.stepId] = 'added';
      summary.added++;
      stepEntries.push({ group: 'steps', type: 'added', id: s.stepId, rightId: s.stepId });
    }
  }

  // ── §4.3 edge matching by semantic key (left endpoints mapped through idMap) ───────────────────────
  const flowEntries = [];
  // Enrich each action edge with its raw list index + component name from the raw document.
  const enrich = (doc, wf, side) => (edge) => {
    if (edge.kind === 'seq') return { ...edge };
    const step = (wf?.steps || []).find((x) => x.stepId === edge.from);
    const rawList = edge.kind === 'failure' ? step?.onFailure : step?.onSuccess;
    const map = rawActionMap(doc, rawList);
    const pos = (edge.order ?? 1) - 1;
    const slot = map[pos] || {};
    return { ...edge, rawIndex: slot.rawIndex, componentName: slot.componentName, reference: slot.reference };
  };
  const leftEdges = leftGraph.edges.map(enrich(leftDoc, leftWf, 'left'));
  const rightEdges = rightGraph.edges.map(enrich(rightDoc, rightWf, 'right'));

  const semKey = (edge, mapEndpoints) => {
    const from = mapEndpoints ? (idMap.get(edge.from) ?? edge.from) : edge.from;
    const to = mapEndpoints ? (idMap.get(edge.to) ?? edge.to) : edge.to;
    return `${edge.kind}|${from}|${to}|${edge.actionName ?? ''}`;
  };
  const bucket = (edges, mapEndpoints) => {
    const m = new Map();
    for (const e of edges) {
      const k = semKey(e, mapEndpoints);
      (m.get(k) || m.set(k, []).get(k)).push(e);
    }
    return m;
  };
  const leftByKey = bucket(leftEdges, true);
  const rightByKey = bucket(rightEdges, false);
  const attrTuple = (e) => JSON.stringify([e.criteriaSummary ?? null, e.order ?? 1, !!e.reusable]);

  const classifyEdge = (leftEdge, rightEdge) => {
    // matched pair: compare criteriaSummary, order, reusable
    const changed = (leftEdge.criteriaSummary ?? null) !== (rightEdge.criteriaSummary ?? null)
      || (leftEdge.order ?? 1) !== (rightEdge.order ?? 1)
      || (!!leftEdge.reusable) !== (!!rightEdge.reusable);
    if (changed) {
      paint.left.edges[leftEdge.id] = 'changed';
      paint.right.edges[rightEdge.id] = 'changed';
      summary.changed++;
      flowEntries.push(edgeEntry('changed', rightEdge, leftEdge));
    }
  };
  const allKeys = new Set([...leftByKey.keys(), ...rightByKey.keys()]);
  for (const key of [...allKeys].sort()) {
    const ls = (leftByKey.get(key) || []).slice();
    const rs = (rightByKey.get(key) || []).slice();
    // pass 1: pair edges whose attribute tuples are equal (fixes deletion-among-parallels, §4.3.2) —
    // an equal-tuple pair is unchanged; removing both from the lists leaves only the genuine deltas.
    for (let li = ls.length - 1; li >= 0; li--) {
      const j = rs.findIndex((r) => attrTuple(r) === attrTuple(ls[li]));
      if (j >= 0) { classifyEdge(ls[li], rs[j]); ls.splice(li, 1); rs.splice(j, 1); }
    }
    // pass 2: occurrence-pair the remainder
    const n = Math.min(ls.length, rs.length);
    for (let i = 0; i < n; i++) classifyEdge(ls[i], rs[i]);
    for (let i = n; i < ls.length; i++) { // left-only ⇒ removed
      paint.left.edges[ls[i].id] = 'removed';
      summary.removed++;
      flowEntries.push(edgeEntry('removed', null, ls[i]));
    }
    for (let i = n; i < rs.length; i++) { // right-only ⇒ added
      paint.right.edges[rs[i].id] = 'added';
      summary.added++;
      flowEntries.push(edgeEntry('added', rs[i], null));
    }
  }

  // ── §4.4 workflow-level surfaces ──────────────────────────────────────────────────────────────────
  const workflowEntries = [];
  if (!eq(leftWf?.inputs, rightWf?.inputs)) {
    paint.left.nodes[START_ID] = 'changed';
    paint.right.nodes[START_ID] = 'changed';
    summary.changed++;
    workflowEntries.push({ group: 'workflow', type: 'changed', area: 'inputs', id: START_ID });
  }
  if (!eq(leftWf?.outputs, rightWf?.outputs)) {
    paint.left.nodes[END_ID] = 'changed';
    paint.right.nodes[END_ID] = 'changed';
    summary.changed++;
    workflowEntries.push({ group: 'workflow', type: 'changed', area: 'outputs', id: END_ID });
  }
  const defaultsChanged = !eq(leftWf?.successActions, rightWf?.successActions) || !eq(leftWf?.failureActions, rightWf?.failureActions);
  if (defaultsChanged) {
    // the defaults card is a dedicated `.defaults` element (data-id="defaults"), painted via a top-level
    // diffState.defaults field — not a node id (§5).
    if ((leftGraph.defaults.successActions.length + leftGraph.defaults.failureActions.length) > 0) paint.left.defaults = 'changed';
    if ((rightGraph.defaults.successActions.length + rightGraph.defaults.failureActions.length) > 0) paint.right.defaults = 'changed';
    summary.changed++;
    const which = [];
    if (!eq(leftWf?.successActions, rightWf?.successActions)) which.push('successActions');
    if (!eq(leftWf?.failureActions, rightWf?.failureActions)) which.push('failureActions');
    workflowEntries.push({ group: 'workflow', type: 'changed', area: 'defaults', id: 'defaults', changedGroups: which });
  }
  // shared $components action bodies referenced by either side (a change invisible to step compare)
  const compRefs = new Map([...referencedComponents(leftDoc, leftWf), ...referencedComponents(rightDoc, rightWf)]);
  for (const path of [...compRefs.keys()].sort()) {
    const [list, name] = path.split('.');
    if (!eq(leftDoc?.components?.[list]?.[name], rightDoc?.components?.[list]?.[name])) {
      summary.changed++;
      workflowEntries.push({ group: 'workflow', type: 'changed', area: 'components', id: `components:${path}`, component: path });
    }
  }
  for (const [key, label] of [['summary', 'summary'], ['description', 'description']]) {
    if (!eq(leftWf?.[key], rightWf?.[key])) {
      summary.changed++;
      workflowEntries.push({ group: 'workflow', type: 'changed', area: key, id: `workflow:${key}` });
    }
  }

  // ── §4.6 shared union layout ──────────────────────────────────────────────────────────────────────
  const layout = buildUnionLayout(leftGraph, rightGraph, idMap);

  return { leftGraph, rightGraph, idMap, steps: stepEntries, flow: flowEntries, workflow: workflowEntries, summary, paint, layout };
}

// A structured flow (edge) change-list entry. `rightEdge`/`leftEdge` are the enriched projection edges.
// It also carries the merge provenance a Take needs (§4.3.6): the action LIST, each side's source step,
// and each side's RAW list index (resolved index ≠ raw index — reusable refs drop from the resolved list).
function edgeEntry(type, rightEdge, leftEdge) {
  const e = rightEdge || leftEdge;
  const list = e.kind === 'seq' ? null : (e.kind === 'failure' ? 'onFailure' : 'onSuccess');
  return {
    group: 'flow',
    type,
    kind: e.kind,
    from: e.from,
    to: e.to,
    actionName: e.actionName ?? null,
    criteriaSummary: e.criteriaSummary ?? null,
    leftId: leftEdge?.id,
    rightId: rightEdge?.id,
    component: (rightEdge?.componentName ?? leftEdge?.componentName) || undefined,
    reference: (rightEdge?.reference ?? leftEdge?.reference) || undefined,
    list,
    leftFrom: leftEdge?.from,
    rightFrom: rightEdge?.from,
    leftRawIndex: leftEdge?.rawIndex,
    rightRawIndex: rightEdge?.rawIndex,
  };
}

// §4.6: union graph in the RIGHT id-space (right nodes ∪ left-only nodes, edges likewise, endpoints
// mapped through idMap), laid out once; split back per side (left through idMap).
function buildUnionLayout(leftGraph, rightGraph, idMap) {
  const mapId = (id) => idMap.get(id) ?? id;
  const unionNodes = [];
  const seen = new Set();
  for (const n of rightGraph.nodes) { unionNodes.push({ id: n.id }); seen.add(n.id); }
  // walk left steps in declaration order, inserting each left-only node right after its cursor
  let cursorIdx = 0;
  for (const n of leftGraph.nodes) {
    const u = mapId(n.id);
    if (seen.has(u)) { cursorIdx = unionNodes.findIndex((x) => x.id === u); continue; }
    unionNodes.splice(cursorIdx + 1, 0, { id: u });
    seen.add(u);
    cursorIdx += 1;
  }
  const edgeKey = (e, side) => {
    const from = side === 'left' ? mapId(e.from) : e.from;
    const to = side === 'left' ? mapId(e.to) : e.to;
    return `${e.kind}|${from}|${to}`;
  };
  const unionEdges = [];
  const edgeSeen = new Set();
  for (const e of rightGraph.edges) { const k = edgeKey(e, 'right'); if (!edgeSeen.has(k)) { edgeSeen.add(k); unionEdges.push({ from: e.from, to: e.to, kind: e.kind }); } }
  for (const e of leftGraph.edges) { const k = edgeKey(e, 'left'); if (!edgeSeen.has(k)) { edgeSeen.add(k); unionEdges.push({ from: mapId(e.from), to: mapId(e.to), kind: e.kind }); } }
  const positions = layoutGraph({ nodes: unionNodes, edges: unionEdges }) || {};
  const left = {};
  for (const n of leftGraph.nodes) { const p = positions[mapId(n.id)]; if (p) left[n.id] = p; }
  const right = {};
  for (const n of rightGraph.nodes) { const p = positions[n.id]; if (p) right[n.id] = p; }
  return { left, right };
}

// §4.7 — overlay mode's data. Fold a diff `result` into ONE graph in the BASE side's id-space: the base
// side rendered solid (its §4.2/§4.3 classification), the OTHER side's exclusive nodes/edges appended as
// translucent ghosts (elements absent from the base version). `base` is 'left' or 'right' — the caller
// picks (§6.2: the merge target when set, else 'right'); the module stays symmetric. Returns a
// projectWorkflow-shaped `graph`, a diffState-ready `paint` (with `ghosts` + `overlay: true`), the union
// `layout` re-keyed to base ids, and an `entryMap` (each side's projection node/edge id → its id on the
// single overlay surface) so the change list can select/centre on the one surface.
export function buildGhostProjection(result, base = 'right') {
  const other = base === 'left' ? 'right' : 'left';
  const baseGraph = base === 'left' ? result.leftGraph : result.rightGraph;
  const otherGraph = base === 'left' ? result.rightGraph : result.leftGraph;
  const basePaint = result.paint[base];
  const otherPaint = result.paint[other];

  // map an OTHER-side projection id into base id-space. base 'right' ⇒ left→right via idMap directly;
  // base 'left' ⇒ right→left via the reverse map. An exclusive id maps to itself (never remapped).
  let toBase;
  if (base === 'right') {
    toBase = (id) => result.idMap.get(id) ?? id;
  } else {
    const rev = new Map();
    for (const [l, r] of result.idMap) rev.set(r, l);
    toBase = (id) => rev.get(id) ?? id;
  }

  const baseNodeIds = new Set(baseGraph.nodes.map((n) => n.id));
  const isWf = (id) => typeof id === 'string' && id.startsWith('workflow:');

  // exclusive other-side nodes: their base-space id is not already a base node (START/END always map).
  const ghostNodeSrc = otherGraph.nodes.filter((n) => !baseNodeIds.has(toBase(n.id)));
  const ghostNodes = ghostNodeSrc.map((n) => ({ ...n, id: toBase(n.id) }));
  // exclusive other-side edges: those the diff painted with the other side's exclusive class.
  const exclusiveClass = other === 'left' ? 'removed' : 'added';
  const ghostEdgeSrc = otherGraph.edges.filter((e) => otherPaint.edges[e.id] === exclusiveClass);
  // `ghost: true` reaches the routing pass (workflow-layout.js routeEdges), which sorts ghosts
  // after solid edges in a shared corridor so a ghost lane never coincides with a solid one.
  const ghostEdges = ghostEdgeSrc.map((e) => ({
    ...e, id: `ghost:${e.id}`, from: toBase(e.from), to: isWf(e.to) ? e.to : toBase(e.to), ghost: true,
  }));

  const graph = { ...baseGraph, nodes: [...baseGraph.nodes, ...ghostNodes], edges: [...baseGraph.edges, ...ghostEdges] };

  // paint, in base id-space: base classification verbatim, then the appended ghosts keep theirs + join
  // the `ghosts` membership lists (the surface renders those translucent, §5).
  const nodePaint = { ...basePaint.nodes };
  const notes = { ...basePaint.notes };
  const ghostNodeIds = [];
  ghostNodeSrc.forEach((n, i) => {
    const gid = ghostNodes[i].id;
    if (otherPaint.nodes[n.id]) nodePaint[gid] = otherPaint.nodes[n.id];
    if (otherPaint.notes?.[n.id]) notes[gid] = otherPaint.notes[n.id];
    ghostNodeIds.push(gid);
  });
  const edgePaint = { ...basePaint.edges };
  const ghostEdgeIds = [];
  ghostEdges.forEach((e) => { edgePaint[e.id] = exclusiveClass; ghostEdgeIds.push(e.id); });
  // ghost-only exit chips (`workflow:X` reached only by ghost edges) — classed like edges (§4.7).
  const baseExitTargets = new Set(baseGraph.edges.filter((e) => isWf(e.to)).map((e) => e.to));
  for (const e of ghostEdges) {
    if (isWf(e.to) && !baseExitTargets.has(e.to) && !ghostEdgeIds.includes(e.to)) ghostEdgeIds.push(e.to);
  }

  const paint = { nodes: nodePaint, edges: edgePaint, notes, ghosts: { nodes: ghostNodeIds, edges: ghostEdgeIds }, overlay: true };
  if (basePaint.defaults) paint.defaults = basePaint.defaults; // a defaults change with no base card is list-only

  // layout = the §4.6 union positions re-keyed to base ids. Both sides came from ONE layout call, so base
  // and ghost positions are already mutually consistent.
  const layout = {};
  for (const n of baseGraph.nodes) { const p = result.layout[base][n.id]; if (p) layout[n.id] = p; }
  ghostNodeSrc.forEach((n, i) => { const p = result.layout[other][n.id]; if (p) layout[ghostNodes[i].id] = p; });

  // entryMap: any projection id (either side) → the overlay surface's id. Base ids map to themselves;
  // other-side node ids map through toBase (matched → the base solid node; exclusive → its ghost id);
  // exclusive other-side edges map to their `ghost:`-prefixed id. A changed edge's base-side id is always
  // present in its change-list entry, so matched other-side edges need no entry.
  const entryMap = new Map();
  for (const n of baseGraph.nodes) entryMap.set(n.id, n.id);
  for (const e of baseGraph.edges) entryMap.set(e.id, e.id);
  for (const n of otherGraph.nodes) entryMap.set(n.id, toBase(n.id));
  ghostEdgeSrc.forEach((e, i) => entryMap.set(e.id, ghostEdges[i].id));

  return { graph, paint, layout, entryMap, base };
}
