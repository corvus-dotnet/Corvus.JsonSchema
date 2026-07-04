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
