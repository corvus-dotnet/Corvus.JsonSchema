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
