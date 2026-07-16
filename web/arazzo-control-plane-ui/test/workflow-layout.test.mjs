// Unit tests for the built-in layered layout (src/workflow-layout.js).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { layoutGraph, NODE_HEIGHT, RANK_GAP } from '../src/workflow-layout.js';
import { projectWorkflow } from '../src/workflow-graph.js';
import { designerFixture } from '../demo/designer-fixture.js';

const RANK_STRIDE = NODE_HEIGHT + RANK_GAP;

test('a linear chain stacks one node per rank, top to bottom', () => {
  const graph = {
    nodes: [{ id: 'a' }, { id: 'b' }, { id: 'c' }],
    edges: [
      { from: 'a', to: 'b', kind: 'seq' },
      { from: 'b', to: 'c', kind: 'seq' },
    ],
  };
  const pos = layoutGraph(graph);
  assert.equal(pos.a.y, 0);
  assert.equal(pos.b.y, RANK_STRIDE);
  assert.equal(pos.c.y, 2 * RANK_STRIDE);
  // A single-column chain is centred: same x throughout.
  assert.equal(pos.a.x, pos.b.x);
  assert.equal(pos.b.x, pos.c.x);
});

test('longest path wins: a skip edge does not pull a node up', () => {
  const graph = {
    nodes: [{ id: 'a' }, { id: 'b' }, { id: 'c' }],
    edges: [
      { from: 'a', to: 'b', kind: 'seq' },
      { from: 'b', to: 'c', kind: 'seq' },
      { from: 'a', to: 'c', kind: 'success' }, // skip edge must not lift c to rank 1
    ],
  };
  const pos = layoutGraph(graph);
  assert.equal(pos.c.y, 2 * RANK_STRIDE);
});

test('a backward goto (loop) does not break ranking', () => {
  const graph = {
    nodes: [{ id: 'a' }, { id: 'b' }],
    edges: [
      { from: 'a', to: 'b', kind: 'seq' },
      { from: 'b', to: 'a', kind: 'failure' }, // loop back — ignored for ranking
    ],
  };
  const pos = layoutGraph(graph);
  assert.equal(pos.a.y, 0);
  assert.equal(pos.b.y, RANK_STRIDE);
});

test('the fixture lays out with every node placed and no overlaps', () => {
  const graph = projectWorkflow(designerFixture, 'place-order');
  const pos = layoutGraph(graph);
  assert.equal(Object.keys(pos).length, graph.nodes.length);
  const seen = new Set();
  for (const { x, y } of Object.values(pos)) {
    const key = `${x},${y}`;
    assert.ok(!seen.has(key), `two nodes share position ${key}`);
    seen.add(key);
  }
  // manual-review is reached by goto edges from authorize-payment (rank 1) and sits after
  // capture-payment in declaration order; it must not sit at rank 0.
  assert.ok(pos['manual-review'].y > 0);
});

test('deterministic: same input, same output', () => {
  const graph = projectWorkflow(designerFixture, 'place-order');
  assert.deepEqual(layoutGraph(graph), layoutGraph(graph));
});

test('a step appended last but wired into the middle of the flow ranks where the flow puts it', () => {
  // The reported case: add a second validate step, divert validate-order to it unconditionally,
  // then goto BACK to authorize-payment (declared earlier). Declaration order: validate-order,
  // authorize-payment, validate-order-2. Flow order: validate-order → validate-order-2 →
  // authorize-payment. The old declaration-order ranking treated the backward goto as invisible
  // and stacked validate-order-2 at the bottom.
  const doc = {
    arazzo: '1.1.0',
    info: { title: 't', version: '1' },
    sourceDescriptions: [],
    workflows: [{
      workflowId: 'wf',
      steps: [
        { stepId: 'validate-order', operationId: 'a', onSuccess: [{ name: 'next', type: 'goto', stepId: 'validate-order-2' }] },
        { stepId: 'authorize-payment', operationId: 'b' },
        { stepId: 'validate-order-2', operationId: 'c', onSuccess: [{ name: 'onward', type: 'goto', stepId: 'authorize-payment' }] },
      ],
    }],
  };
  const graph = projectWorkflow(doc, 'wf');
  const positions = layoutGraph(graph);
  assert.ok(positions['validate-order'].y < positions['validate-order-2'].y,
    'the new step sits below its flow predecessor');
  assert.ok(positions['validate-order-2'].y < positions['authorize-payment'].y,
    'authorize-payment sits below the step that flows into it, despite earlier declaration');
});

test('a retry loop back up the flow does not destroy the ranking', () => {
  const doc = {
    arazzo: '1.1.0',
    info: { title: 't', version: '1' },
    sourceDescriptions: [],
    workflows: [{
      workflowId: 'wf',
      steps: [
        { stepId: 'first', operationId: 'a' },
        { stepId: 'second', operationId: 'b', onFailure: [{ name: 'again', type: 'goto', stepId: 'first', criteria: [{ condition: '$statusCode == 429' }] }] },
        { stepId: 'third', operationId: 'c' },
      ],
    }],
  };
  const positions = layoutGraph(projectWorkflow(doc, 'wf'));
  assert.ok(positions.first.y < positions.second.y && positions.second.y < positions.third.y,
    'the loop edge is drawn but never ranks a predecessor below its successor');
});

// ── Edge routing (routeEdges, §6.3 corridor/lane pass) ──────────────────────────────────────────

import { routeEdges, NODE_WIDTH, LANE_PITCH, UP_LANE_PITCH, CORRIDOR_MARGIN } from '../src/workflow-layout.js';

/** A linear chain a→b→c→d with a skip edge and a ghost skip edge — the overlay's shape. */
function chainWithSkips() {
  const graph = {
    nodes: [{ id: 'a' }, { id: 'b' }, { id: 'c' }, { id: 'd' }],
    edges: [
      { id: 'e-ab', from: 'a', to: 'b', kind: 'seq' },
      { id: 'e-bc', from: 'b', to: 'c', kind: 'seq' },
      { id: 'e-cd', from: 'c', to: 'd', kind: 'seq' },
      { id: 'e-ad', from: 'a', to: 'd', kind: 'failure' },
      { id: 'ghost:e-bd', from: 'b', to: 'd', kind: 'seq', ghost: true },
    ],
  };
  return { graph, positions: layoutGraph(graph) };
}

test('routeEdges: adjacent-rank edges get no route; band-skipping edges get one waypoint per crossed band', () => {
  const { graph, positions } = chainWithSkips();
  const routes = routeEdges(graph, positions);
  assert.equal(routes['e-ab'], undefined);
  assert.equal(routes['e-bc'], undefined);
  assert.equal(routes['e-cd'], undefined);
  assert.equal(routes['e-ad'].kind, 'down');
  assert.equal(routes['e-ad'].points.length, 2); // crosses bands 1 and 2
  assert.equal(routes['ghost:e-bd'].points.length, 1); // crosses band 2
});

test('routeEdges: a skip edge routes through a corridor BESIDE the column, not through it', () => {
  const { graph, positions } = chainWithSkips();
  const routes = routeEdges(graph, positions);
  const colCentre = positions.b.x + NODE_WIDTH / 2;
  for (const wp of routes['e-ad'].points) {
    // Clear of the node cards it used to cut through: outside [x, x+width].
    assert.ok(wp.x <= positions.b.x || wp.x >= positions.b.x + NODE_WIDTH,
      `waypoint ${wp.x} must sit beside the column centred on ${colCentre}`);
  }
});

test('routeEdges: edges sharing a corridor take distinct lanes, ghosts after solids, deterministically', () => {
  const { graph, positions } = chainWithSkips();
  const routes = routeEdges(graph, positions);
  // Both skip edges cross band 2 in the same corridor: distinct lane x, exactly one pitch apart.
  const solid = routes['e-ad'].points[1];
  const ghost = routes['ghost:e-bd'].points[0];
  assert.equal(solid.y, ghost.y);
  assert.notEqual(solid.x, ghost.x);
  assert.equal(Math.abs(solid.x - ghost.x), LANE_PITCH);
  assert.ok(ghost.x > solid.x, 'the ghost sorts after the solid edge');
  // Deterministic: a second run yields identical routes.
  assert.deepEqual(routeEdges(graph, positions), routes);
});

test('routeEdges: overlapping upward loops take distinct right-side lanes; disjoint ones may share', () => {
  const graph = {
    nodes: [{ id: 'a' }, { id: 'b' }, { id: 'c' }, { id: 'd' }],
    edges: [
      { id: 'e-ab', from: 'a', to: 'b', kind: 'seq' },
      { id: 'e-bc', from: 'b', to: 'c', kind: 'seq' },
      { id: 'e-cd', from: 'c', to: 'd', kind: 'seq' },
      { id: 'loop-ca', from: 'c', to: 'a', kind: 'failure' },
      { id: 'loop-db', from: 'd', to: 'b', kind: 'failure' }, // overlaps loop-ca in y
    ],
  };
  const positions = layoutGraph(graph);
  const routes = routeEdges(graph, positions);
  assert.equal(routes['loop-ca'].kind, 'up');
  assert.equal(routes['loop-db'].kind, 'up');
  const [xa, xb] = [routes['loop-ca'].points[0].x, routes['loop-db'].points[0].x];
  assert.equal(Math.abs(xa - xb), UP_LANE_PITCH, 'overlapping loops sit one lane pitch apart');
  // Each lane clears the rightmost node it spans.
  const rightmost = Math.max(...Object.values(positions).map((p) => p.x)) + NODE_WIDTH;
  assert.ok(Math.min(xa, xb) >= rightmost + CORRIDOR_MARGIN);
});

test('routeEdges: the onboard-customer overlay union — the goto and the chain share no corridor line', () => {
  // v2 chain createAccount→verifyIdentity→provisionResources→notifyApplicant, the v2 goto
  // verifyIdentity→notifyApplicant, and v1's ghost tail provisionResources→sendWelcome.
  const graph = {
    nodes: [{ id: 'createAccount' }, { id: 'verifyIdentity' }, { id: 'provisionResources' },
      { id: 'notifyApplicant' }, { id: 'sendWelcome' }],
    edges: [
      { id: 's1', from: 'createAccount', to: 'verifyIdentity', kind: 'seq' },
      { id: 's2', from: 'verifyIdentity', to: 'provisionResources', kind: 'seq' },
      { id: 's3', from: 'provisionResources', to: 'notifyApplicant', kind: 'seq' },
      { id: 'goto', from: 'verifyIdentity', to: 'notifyApplicant', kind: 'failure' },
      { id: 'ghost:tail', from: 'provisionResources', to: 'sendWelcome', kind: 'seq', ghost: true },
    ],
  };
  const positions = layoutGraph(graph);
  const routes = routeEdges(graph, positions);
  // The goto crosses provisionResources' band and must clear its card.
  const wp = routes.goto.points[0];
  const pr = positions.provisionResources;
  assert.ok(wp.x <= pr.x || wp.x >= pr.x + NODE_WIDTH, 'the goto lane clears provisionResources');
  // The adjacent chain edges keep their no-route straight paths.
  assert.equal(routes.s1, undefined);
  assert.equal(routes.s2, undefined);
});

test('routeEdges: bands derive from actual positions, so manual moves re-band instead of crashing', () => {
  const { graph, positions } = chainWithSkips();
  // Drag b far below d: the chain's bands reorder; routing still returns clean data.
  const moved = { ...positions, b: { x: positions.b.x + 400, y: positions.d.y + 300 } };
  const routes = routeEdges(graph, moved);
  for (const route of Object.values(routes)) {
    assert.ok(['down', 'up'].includes(route.kind));
    for (const wp of route.points) {
      assert.ok(Number.isFinite(wp.x) && Number.isFinite(wp.y));
    }
  }
});

test('routeEdges: a chord that clears the intermediate band stays perfectly straight (no corridor pull)', () => {
  // Three columns at ranks 0 and 2, one lone node at rank 1: the a3→c3 chord passes well clear of
  // b, so its waypoint must sit ON the chord (x = the shared column centre), not snap to a corridor.
  const graph = {
    nodes: [{ id: 'a' }, { id: 'a2' }, { id: 'a3' }, { id: 'b' }, { id: 'c' }, { id: 'c2' }, { id: 'c3' }],
    edges: [
      { id: 'e1', from: 'a', to: 'b', kind: 'seq' },
      { id: 'e2', from: 'a2', to: 'b', kind: 'seq' },
      { id: 'e3', from: 'a3', to: 'b', kind: 'seq' },
      { id: 'e4', from: 'b', to: 'c', kind: 'seq' },
      { id: 'e5', from: 'b', to: 'c2', kind: 'seq' },
      { id: 'e6', from: 'b', to: 'c3', kind: 'seq' },
      { id: 'vertical', from: 'a3', to: 'c3', kind: 'failure' },
      { id: 'diagonal', from: 'a3', to: 'c2', kind: 'failure' },
    ],
  };
  const positions = layoutGraph(graph);
  const routes = routeEdges(graph, positions);
  const cxOf = (id) => positions[id].x + NODE_WIDTH / 2;
  // Same-column chord: the waypoint is exactly the column centre — dead straight.
  assert.equal(routes.vertical.points.length, 1);
  assert.equal(routes.vertical.points[0].x, cxOf('a3'));
  assert.equal(cxOf('a3'), cxOf('c3'));
  // Diagonal chord: the waypoint is the chord's own crossing point (midway between the centres).
  assert.ok(Math.abs(routes.diagonal.points[0].x - (cxOf('a3') + cxOf('c2')) / 2) < 0.001,
    `diagonal waypoint ${routes.diagonal.points[0].x} sits on the chord`);
  // Neither was pulled to a corridor candidate beside b.
  const bRight = positions.b.x + NODE_WIDTH + 36;
  assert.notEqual(routes.vertical.points[0].x, bRight);
  assert.notEqual(routes.diagonal.points[0].x, bRight);
});

test('routeEdges: an adjacent-band horizontally-disjoint pair takes ONE direct facing-border curve', () => {
  // The screenshot case: provisionResources dragged up-left beside createAccount; the
  // verifyIdentity→provisionResources edge must NOT climb a right lane and run back across
  // the canvas — it goes directly from verifyIdentity's left border into provisionResources'
  // right border.
  const graph = {
    nodes: [{ id: 'pR' }, { id: 'cA' }, { id: 'vI' }, { id: 'nA' }],
    edges: [
      { id: 'seq1', from: 'cA', to: 'vI', kind: 'seq' },
      { id: 'back', from: 'vI', to: 'pR', kind: 'seq' },
      { id: 'seq3', from: 'pR', to: 'nA', kind: 'seq' },
    ],
  };
  const positions = { pR: { x: 0, y: 0 }, cA: { x: 560, y: 0 }, vI: { x: 560, y: 220 }, nA: { x: 560, y: 440 } };
  const routes = routeEdges(graph, positions);
  assert.equal(routes.back.kind, 'up');
  assert.equal(routes.back.direct, true, 'adjacent + disjoint + clear box → direct');
  assert.equal(routes.back.side, 'left', 'the target is to the left');
});

test('routeEdges: a blocked bounding box falls back to a lane instead of cutting through', () => {
  const graph = {
    nodes: [{ id: 'target' }, { id: 'blocker' }, { id: 'source' }],
    edges: [{ id: 'lat', from: 'source', to: 'target', kind: 'failure' }],
  };
  // target ← blocker ← source on one band: the direct curve would cross the blocker.
  const positions = { target: { x: 0, y: 0 }, blocker: { x: 260, y: 0 }, source: { x: 520, y: 0 } };
  const routes = routeEdges(graph, positions);
  assert.equal(routes.lat.kind, 'up');
  assert.ok(!routes.lat.direct, 'blocked box → no direct curve');
});

test('routeEdges: a multi-band up-left target routes up the LEFT side (crossing penalty beats the fixed right lane)', () => {
  const graph = {
    nodes: [{ id: 'pR' }, { id: 'cA' }, { id: 'vI' }, { id: 'nA' }],
    edges: [
      { id: 'seq1', from: 'cA', to: 'vI', kind: 'seq' },
      { id: 'seq2', from: 'vI', to: 'nA', kind: 'seq' },
      { id: 'far', from: 'nA', to: 'pR', kind: 'failure' },
    ],
  };
  // pR sits top-left beside cA; nA is two bands below — a right lane's horizontal leg into pR
  // would cross cA, so the route must take the left side.
  const positions = { pR: { x: 0, y: 0 }, cA: { x: 560, y: 0 }, vI: { x: 560, y: 220 }, nA: { x: 560, y: 440 } };
  const routes = routeEdges(graph, positions);
  assert.equal(routes.far.kind, 'up');
  assert.equal(routes.far.side, 'left');
  assert.ok(routes.far.points[0].x < 0, `lane ${routes.far.points[0].x} sits left of the whole span`);
});
