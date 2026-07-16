// Workflow graph layout — pure data in, pure data out (design: workflow-designer-design.md §6.3).
//
// The built-in layered layout for the design surface: rank nodes by longest path over the ACTUAL
// flow edges (sequence + success/failure gotos — not declaration order, so a step dropped last but
// wired into the middle of the flow ranks where the flow puts it), break genuine loops (retry /
// goto back up the flow) by dropping DFS back-edges from ranking only, order each rank by the mean
// column of its predecessors (parents above their children), and centre each rank. Deterministic
// and dependency-free, so the surface works offline; a host that wants dagre-quality layout injects
// it through <arazzo-design-surface>.layoutEngine and this module is bypassed. Manual node
// positions (designerState) override whatever the engine produced.
//
//   const positions = layoutGraph(graph, { nodeWidth, nodeHeight });  // → { [nodeId]: {x, y} }

/** Layout constants shared with the renderer (world-coordinate units). */
export const NODE_WIDTH = 210;
export const NODE_HEIGHT = 74;
export const RANK_GAP = 64;
export const COLUMN_GAP = 48;

/**
 * Compute positions for every node of a projected graph.
 *
 * @param {{nodes: {id: string}[], edges: {from: string, to: string, kind: string}[]}} graph
 * @param {{nodeWidth?: number, nodeHeight?: number, rankGap?: number, columnGap?: number}} [opts]
 * @returns {Record<string, {x: number, y: number}>} Top-left positions keyed by node id.
 */
export function layoutGraph(graph, opts = {}) {
  const nodeWidth = opts.nodeWidth ?? NODE_WIDTH;
  const nodeHeight = opts.nodeHeight ?? NODE_HEIGHT;
  const rankGap = opts.rankGap ?? RANK_GAP;
  const columnGap = opts.columnGap ?? COLUMN_GAP;

  const ids = graph.nodes.map((n) => n.id);
  const declIndex = new Map(ids.map((id, i) => [id, i]));
  const edges = graph.edges.filter((e) => declIndex.has(e.from) && declIndex.has(e.to) && e.from !== e.to);
  const outEdges = new Map(ids.map((id) => [id, []]));
  for (const e of edges) outEdges.get(e.from).push(e);

  // 1. Break loops: DFS in declaration order (start first); an edge whose target is on the current
  //    DFS stack closes a cycle (retry, goto back up the flow) and is excluded from RANKING only —
  //    the renderer still draws it.
  const state = new Map(); // 0 unvisited · 1 on-stack · 2 done
  const kept = [];
  const visit = (id) => {
    state.set(id, 1);
    for (const e of outEdges.get(id)) {
      const s = state.get(e.to) ?? 0;
      if (s === 1) continue;
      kept.push(e);
      if (s === 0) visit(e.to);
    }
    state.set(id, 2);
  };
  for (const id of ids) if ((state.get(id) ?? 0) === 0) visit(id);

  // 2. Longest-path ranks over the acyclic remainder (Kahn order, deterministic).
  const keptOut = new Map(ids.map((id) => [id, []]));
  const inDegree = new Map(ids.map((id) => [id, 0]));
  for (const e of kept) {
    keptOut.get(e.from).push(e.to);
    inDegree.set(e.to, inDegree.get(e.to) + 1);
  }
  const rank = new Map(ids.map((id) => [id, 0]));
  const queue = ids.filter((id) => inDegree.get(id) === 0);
  while (queue.length) {
    const id = queue.shift();
    for (const to of keptOut.get(id)) {
      rank.set(to, Math.max(rank.get(to), rank.get(id) + 1));
      inDegree.set(to, inDegree.get(to) - 1);
      if (inDegree.get(to) === 0) queue.push(to);
    }
  }

  // A disconnected node (no ranked edge in or out) still deserves its own rank below the node it
  // follows in declaration order, rather than stacking every orphan at rank 0.
  const hasEdge = new Set(kept.flatMap((e) => [e.from, e.to]));
  for (let i = 1; i < ids.length; i++) {
    if (!hasEdge.has(ids[i])) {
      rank.set(ids[i], Math.max(rank.get(ids[i]), rank.get(ids[i - 1]) + 1));
    }
  }

  // 3. Group by rank; order each rank by the mean column of its ranked predecessors (parents sit
  //    above their children), tie-breaking on declaration order. Then centre each rank on x = 0.
  const ranks = new Map();
  for (const id of ids) {
    const r = rank.get(id);
    if (!ranks.has(r)) ranks.set(r, []);
    ranks.get(r).push(id);
  }

  const inKept = new Map(ids.map((id) => [id, []]));
  for (const e of kept) inKept.get(e.to).push(e.from);
  const column = new Map();
  const positions = {};
  for (const [r, members] of [...ranks.entries()].sort((a, b) => a[0] - b[0])) {
    const score = (id) => {
      const parents = inKept.get(id).filter((p) => rank.get(p) < r && column.has(p));
      if (!parents.length) return Number.MAX_SAFE_INTEGER; // parentless: keep declaration position
      return parents.reduce((sum, p) => sum + column.get(p), 0) / parents.length;
    };
    members.sort((a, b) => (score(a) - score(b)) || (declIndex.get(a) - declIndex.get(b)));
    members.forEach((id, i) => column.set(id, i));

    const rowWidth = members.length * nodeWidth + (members.length - 1) * columnGap;
    members.forEach((id, i) => {
      positions[id] = {
        x: Math.round(i * (nodeWidth + columnGap) - rowWidth / 2),
        y: r * (nodeHeight + rankGap),
      };
    });
  }
  return positions;
}
