// Workflow graph layout — pure data in, pure data out (design: workflow-designer-design.md §6.3).
//
// The built-in layered layout for the design surface: rank steps by longest path over their
// sequence/success edges (top-to-bottom), keep declaration order within a rank, and centre each
// rank. Deterministic and dependency-free, so the surface works offline; a host that wants
// dagre-quality layout injects it through <arazzo-design-surface>.layoutEngine and this module is
// bypassed. Manual node positions (designerState) override whatever the engine produced.
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
  const index = new Map(ids.map((id, i) => [id, i]));

  // Rank = longest path from any root, following forward edges only (a goto back up the document
  // is a loop; ranking must not chase it). Forward = target declared after source.
  const forward = graph.edges.filter(
    (e) => index.has(e.from) && index.has(e.to) && index.get(e.to) > index.get(e.from),
  );
  const rank = new Map(ids.map((id) => [id, 0]));
  // Declaration order is a topological order for forward edges, so one pass suffices.
  for (const id of ids) {
    for (const e of forward) {
      if (e.from !== id) continue;
      rank.set(e.to, Math.max(rank.get(e.to), rank.get(id) + 1));
    }
  }
  // A disconnected step (no forward edge in or out) still deserves its own rank below the flow it
  // follows in declaration order, rather than stacking every orphan at rank 0.
  const hasEdge = new Set(forward.flatMap((e) => [e.from, e.to]));
  for (let i = 1; i < ids.length; i++) {
    if (!hasEdge.has(ids[i])) {
      rank.set(ids[i], Math.max(rank.get(ids[i]), rank.get(ids[i - 1]) + 1));
    }
  }

  // Group by rank, preserving declaration order; centre each rank around x = 0.
  const ranks = new Map();
  for (const id of ids) {
    const r = rank.get(id);
    if (!ranks.has(r)) ranks.set(r, []);
    ranks.get(r).push(id);
  }

  const positions = {};
  for (const [r, members] of [...ranks.entries()].sort((a, b) => a[0] - b[0])) {
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
