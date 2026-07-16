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

/** Edge-routing constants (world-coordinate units). */
export const LANE_PITCH = 16;
export const UP_LANE_PITCH = 18;
export const CORRIDOR_MARGIN = 36;
const MIN_GAP_FOR_CORRIDOR = 24;
const STRAIGHT_CLEARANCE = 18;

/**
 * Route edges around the laid-out nodes (design: workflow-designer-design.md §6.3). Pure data in,
 * pure data out, like `layoutGraph`. Only the edges that NEED waypoints get a route:
 *
 * - A downward edge spanning ≥2 rank bands gets one waypoint per crossed band. **Straight wins:**
 *   when the source→target chord already clears the band's nodes (`STRAIGHT_CLEARANCE`), the
 *   waypoint sits ON the chord, so an edge that can run straight does. Only a chord that would
 *   pierce a node snaps to an **edge corridor** — the gap between adjacent nodes of that band (or
 *   just outside the row) nearest the chord. Edges whose waypoints land together in a band
 *   (corridor-sharers, or near-identical chords) take distinct **edge lanes**: deterministic slots
 *   `LANE_PITCH` apart around the cluster, ghosts sorted after solid ones so a ghost never
 *   coincides.
 * - An upward or same-band edge (retry / goto back up the flow) gets a right-side vertical lane,
 *   lane-separated by greedy interval colouring over overlapping vertical spans — replacing the
 *   old fixed-width bow that stacked concurrent loops onto one curve.
 *
 * Adjacent-rank downward edges need no route (distinct borders already separate them) and get no
 * entry. Bands are derived by clustering the ACTUAL y positions, so routing survives manual node
 * moves and injected layout engines alike. Edges into exit chips (`workflow:<id>` targets) are the
 * renderer's own synthetic geometry and are skipped here.
 *
 * @param {{nodes: {id: string}[], edges: {id: string, from: string, to: string, ghost?: boolean}[]}} graph
 * @param {Record<string, {x: number, y: number}>} positions Top-left node positions (post-override).
 * @param {{nodeWidth?: number, nodeHeight?: number}} [opts]
 * @returns {Record<string, {kind: 'down'|'up', points: {x: number, y: number}[]}>} Routes by edge id.
 */
export function routeEdges(graph, positions, opts = {}) {
  const nodeWidth = opts.nodeWidth ?? NODE_WIDTH;
  const nodeHeight = opts.nodeHeight ?? NODE_HEIGHT;

  const placed = graph.nodes.filter((n) => positions[n.id]);
  if (!placed.length) return {};

  // 1. Cluster nodes into horizontal bands by y (exact ranks under the built-in layout; nearest
  //    band after manual moves). A new band starts when a node clears the previous band's extent.
  const byY = [...placed].sort((a, b) => (positions[a.id].y - positions[b.id].y) || (positions[a.id].x - positions[b.id].x));
  const bands = [];
  const bandOf = new Map();
  for (const node of byY) {
    const y = positions[node.id].y;
    const current = bands[bands.length - 1];
    if (!current || y > current.bottom - nodeHeight * 0.4) {
      bands.push({ top: y, bottom: y + nodeHeight, members: [node.id] });
    } else {
      current.members.push(node.id);
      current.top = Math.min(current.top, y);
      current.bottom = Math.max(current.bottom, y + nodeHeight);
    }
    bandOf.set(node.id, bands.length - 1);
  }
  const bandCenter = (b) => (bands[b].top + bands[b].bottom) / 2;
  const rowIntervals = bands.map((band) =>
    band.members.map((id) => positions[id].x).sort((p, q) => p - q).map((x) => [x, x + nodeWidth]));

  // 2. Straight wins: `wantX` (where the chord crosses the band) stands when it clears every node
  //    interval by STRAIGHT_CLEARANCE. Otherwise snap to the nearest corridor candidate — outside
  //    either end of the row, or the centre of any wide-enough gap between adjacent nodes.
  const clearsRow = (b, x) =>
    rowIntervals[b].every(([s, e]) => x <= s - STRAIGHT_CLEARANCE || x >= e + STRAIGHT_CLEARANCE);
  const corridorX = (b, wantX) => {
    if (clearsRow(b, wantX)) return wantX;
    const row = rowIntervals[b];
    const candidates = [row[0][0] - CORRIDOR_MARGIN, row[row.length - 1][1] + CORRIDOR_MARGIN];
    for (let i = 1; i < row.length; i++) {
      const gap = row[i][0] - row[i - 1][1];
      if (gap >= MIN_GAP_FOR_CORRIDOR) candidates.push((row[i - 1][1] + row[i][0]) / 2);
    }
    return candidates.reduce((best, c) => (Math.abs(c - wantX) < Math.abs(best - wantX) ? c : best));
  };

  const cx = (id) => positions[id].x + nodeWidth / 2;
  const cy = (id) => positions[id].y + nodeHeight / 2;
  const routes = {};

  // 3. Downward band-skipping edges: a waypoint per crossed band. Per band, crossings landing
  //    within a lane pitch of one another cluster (corridor-sharers, near-identical chords) and
  //    spread into lanes around the cluster's centre; a lone crossing keeps its exact x — a clear
  //    chord stays perfectly straight. Ghosts sort last so a ghost lane never coincides.
  const crossings = new Map(); // band -> [{edge, x}]
  const longEdges = [];
  for (const edge of graph.edges) {
    if (!positions[edge.from] || !positions[edge.to] || edge.from === edge.to) continue;
    const bf = bandOf.get(edge.from);
    const bt = bandOf.get(edge.to);
    if (bt > bf + 1) longEdges.push({ edge, bf, bt });
  }
  for (const { edge, bf, bt } of longEdges) {
    const x1 = cx(edge.from);
    const y1 = cy(edge.from);
    const dx = cx(edge.to) - x1;
    const dy = cy(edge.to) - y1 || 1;
    for (let b = bf + 1; b < bt; b++) {
      const want = x1 + dx * ((bandCenter(b) - y1) / dy);
      if (!crossings.has(b)) crossings.set(b, []);
      crossings.get(b).push({ edge, x: corridorX(b, want) });
    }
  }
  const slotted = new Map(); // `${edgeId}@${band}` -> x
  for (const [band, entries] of crossings) {
    entries.sort((p, q) => (p.x - q.x) || (p.edge.id < q.edge.id ? -1 : 1));
    let cluster = [];
    const flush = () => {
      cluster.sort((p, q) => (!!p.edge.ghost - !!q.edge.ghost)
        || (cx(p.edge.from) - cx(q.edge.from)) || (cx(p.edge.to) - cx(q.edge.to))
        || (p.edge.id < q.edge.id ? -1 : 1));
      const centre = cluster.reduce((sum, e) => sum + e.x, 0) / cluster.length;
      cluster.forEach((entry, i) => {
        slotted.set(`${entry.edge.id}@${band}`,
          cluster.length === 1 ? entry.x : centre + (i - (cluster.length - 1) / 2) * LANE_PITCH);
      });
      cluster = [];
    };
    for (const entry of entries) {
      if (cluster.length && entry.x - cluster[cluster.length - 1].x > LANE_PITCH) flush();
      cluster.push(entry);
    }
    if (cluster.length) flush();
  }
  for (const { edge, bf, bt } of longEdges) {
    const points = [];
    for (let b = bf + 1; b < bt; b++) {
      points.push({ x: slotted.get(`${edge.id}@${b}`), y: bandCenter(b) });
    }
    routes[edge.id] = { kind: 'down', points };
  }

  // 4. Upward and same-band edges. Anchors and side are chosen geometrically, not fixed:
  //    - **Direct lateral**: adjacent/same band, horizontally disjoint, and nothing inside the
  //      endpoints' bounding box → one facing-border-to-facing-border curve, no lane at all.
  //    - Otherwise a vertical lane on the CHEAPER side: each side's cost is its two horizontal
  //      legs (source band and target band), with a heavy penalty for every node a leg would
  //      cross — so a far-left target routes up the left, never right-then-all-the-way-back.
  //    Same-side lanes are separated by greedy interval colouring over overlapping y-spans.
  const nodeRect = (id) => ({ x: positions[id].x, y: positions[id].y, w: nodeWidth, h: nodeHeight });
  const legCost = (band, fromX, toX, ...exclude) => {
    const lo = Math.min(fromX, toX);
    const hi = Math.max(fromX, toX);
    let crossed = 0;
    for (const id of bands[band].members) {
      if (exclude.includes(id)) continue;
      const r = nodeRect(id);
      if (lo < r.x + r.w && r.x < hi) crossed++;
    }
    return (hi - lo) + crossed * 10_000;
  };
  const upward = graph.edges
    .filter((e) => positions[e.from] && positions[e.to] && e.from !== e.to && bandOf.get(e.to) <= bandOf.get(e.from))
    .map((e) => {
      const a = positions[e.from];
      const b = positions[e.to];
      const bf = bandOf.get(e.from);
      const bt = bandOf.get(e.to);
      // Direct lateral: nothing between the facing borders, in either dimension.
      if (bf - bt <= 1) {
        const targetLeft = b.x + nodeWidth + MIN_GAP_FOR_CORRIDOR <= a.x;
        const targetRight = b.x >= a.x + nodeWidth + MIN_GAP_FOR_CORRIDOR;
        if (targetLeft || targetRight) {
          // The curve occupies only the space BETWEEN the facing borders (horizontal tangents keep
          // it inside [cyA, cyB] vertically) — the endpoints' own cards are not obstacles.
          const box = targetLeft
            ? { lo: b.x + nodeWidth, hi: a.x, top: Math.min(cy(e.from), cy(e.to)), bottom: Math.max(cy(e.from), cy(e.to)) }
            : { lo: a.x + nodeWidth, hi: b.x, top: Math.min(cy(e.from), cy(e.to)), bottom: Math.max(cy(e.from), cy(e.to)) };
          const blocked = placed.some((n) => {
            if (n.id === e.from || n.id === e.to) return false;
            const r = nodeRect(n.id);
            return r.x < box.hi && box.lo < r.x + r.w && r.y < box.bottom && box.top < r.y + r.h;
          });
          if (!blocked) return { e, direct: true, side: targetLeft ? 'left' : 'right' };
        }
      }
      const lo = Math.min(bt, bf);
      const hi = Math.max(bt, bf);
      let left = Infinity;
      let right = -Infinity;
      for (let b2 = lo; b2 <= hi; b2++) {
        left = Math.min(left, rowIntervals[b2][0][0]);
        right = Math.max(right, rowIntervals[b2][rowIntervals[b2].length - 1][1]);
      }
      const laneL = left - CORRIDOR_MARGIN;
      const laneR = right + CORRIDOR_MARGIN;
      const costL = legCost(bf, a.x, laneL, e.from) + legCost(bt, b.x, laneL, e.to);
      const costR = legCost(bf, a.x + nodeWidth, laneR, e.from) + legCost(bt, b.x + nodeWidth, laneR, e.to);
      const side = costL < costR ? 'left' : 'right';
      return {
        e, side, laneBase: side === 'left' ? laneL : laneR,
        yTop: Math.min(cy(e.from), cy(e.to)), yBottom: Math.max(cy(e.from), cy(e.to)),
      };
    });
  for (const entry of upward.filter((u) => u.direct)) {
    routes[entry.e.id] = { kind: 'up', direct: true, side: entry.side, points: [] };
  }
  for (const side of ['left', 'right']) {
    const laneEnds = []; // per lane: the largest yBottom taken so far
    const group = upward.filter((u) => !u.direct && u.side === side)
      .sort((p, q) => (p.yTop - q.yTop) || (p.yBottom - q.yBottom) || (!!p.e.ghost - !!q.e.ghost) || (p.e.id < q.e.id ? -1 : 1));
    for (const entry of group) {
      let lane = laneEnds.findIndex((end) => entry.yTop > end);
      if (lane === -1) lane = laneEnds.push(-Infinity) - 1;
      laneEnds[lane] = entry.yBottom;
      const x = entry.laneBase + (side === 'left' ? -1 : 1) * lane * UP_LANE_PITCH;
      routes[entry.e.id] = { kind: 'up', side, points: [{ x, y: cy(entry.e.from) }, { x, y: cy(entry.e.to) }] };
    }
  }

  return routes;
}
