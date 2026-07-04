// Unit tests for the workflow graph projection (src/workflow-graph.js) — the DOM-free layer the
// design surface renders. Run via `node --test` (no browser needed).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { projectWorkflow, listWorkflows } from '../src/workflow-graph.js';
import { designerFixture } from '../demo/designer-fixture.js';

const graph = () => projectWorkflow(designerFixture, 'place-order');

test('listWorkflows names every workflow with its summary', () => {
  const workflows = listWorkflows(designerFixture);
  assert.deepEqual(workflows.map((w) => w.workflowId), ['place-order', 'order-with-compensation']);
  assert.ok(workflows[0].summary.length > 0);
});

test('one node per step, in declared order', () => {
  const g = graph();
  assert.deepEqual(
    g.nodes.map((n) => n.id),
    ['validate-order', 'authorize-payment', 'await-confirmation', 'capture-payment', 'manual-review'],
  );
});

test('binding kinds: operation, channel, and sub-workflow', () => {
  const g = graph();
  assert.equal(g.nodes.find((n) => n.id === 'validate-order').kind, 'operation');
  const channel = g.nodes.find((n) => n.id === 'await-confirmation');
  assert.equal(channel.kind, 'channel');
  assert.equal(channel.binding.action, 'receive');
  assert.equal(channel.binding.timeout, 300000);
  assert.match(channel.sublabel, /receive \/channels\/orderConfirmations/);

  const sub = projectWorkflow(designerFixture, 'order-with-compensation');
  assert.equal(sub.nodes.find((n) => n.id === 'run-order').kind, 'workflow');
  assert.equal(sub.nodes.find((n) => n.id === 'run-order').binding.workflowId, 'place-order');
});

test('sequence edges connect consecutive steps but stop at unconditional end/goto', () => {
  const g = graph();
  const seq = g.edges.filter((e) => e.kind === 'seq');
  // capture-payment has an unconditional local `end`, so no fall-through to manual-review.
  assert.deepEqual(
    seq.map((e) => `${e.from}->${e.to}`),
    ['validate-order->authorize-payment', 'authorize-payment->await-confirmation', 'await-confirmation->capture-payment'],
  );
});

test('local goto failure actions become failure edges with criteria summaries', () => {
  const g = graph();
  const decline = g.edges.find((e) => e.kind === 'failure' && e.actionName === 'manual-review-on-decline');
  assert.ok(decline, 'the decline goto is an edge');
  assert.equal(decline.from, 'authorize-payment');
  assert.equal(decline.to, 'manual-review');
  assert.equal(decline.criteriaSummary, '$statusCode == 402');
});

test('reusable component actions resolve and are flagged', () => {
  const g = graph();
  const escalate = g.edges.find((e) => e.actionName === 'escalate');
  assert.ok(escalate, 'the $components reference resolved to an edge');
  assert.equal(escalate.kind, 'failure');
  assert.equal(escalate.to, 'manual-review');
  assert.equal(escalate.reusable, true);
  assert.deepEqual(g.problems, [], 'no problems in the well-formed fixture');
});

test('retry failure actions become a node badge, not an edge', () => {
  const g = graph();
  const node = g.nodes.find((n) => n.id === 'authorize-payment');
  assert.deepEqual(node.retry, { retryAfter: 5, retryLimit: 3, actionName: 'retry-throttled' });
  assert.ok(!g.edges.some((e) => e.actionName === 'retry-throttled'), 'retry is not an edge');
});

test('end actions mark the node terminal', () => {
  const g = graph();
  assert.equal(g.nodes.find((n) => n.id === 'capture-payment').end, true);
  assert.equal(g.nodes.find((n) => n.id === 'manual-review').end, true);
  assert.equal(g.nodes.find((n) => n.id === 'validate-order').end, false);
});

test('the inherited-defaults layer: workflow failureActions + per-node ghost flags', () => {
  const g = graph();
  assert.deepEqual(g.defaults.failureActions.map((a) => a.name), ['give-up']);
  assert.deepEqual(g.defaults.successActions, []);
  // validate-order has no local onFailure → inherits the defaults layer.
  assert.equal(g.nodes.find((n) => n.id === 'validate-order').usesDefaultFailure, true);
  // authorize-payment has local onFailure → overrides.
  assert.equal(g.nodes.find((n) => n.id === 'authorize-payment').usesDefaultFailure, false);
  // No workflow-level successActions → nothing to inherit.
  assert.equal(g.nodes.find((n) => n.id === 'validate-order').usesDefaultSuccess, false);
});

test('criteria and output counts land on the node', () => {
  const g = graph();
  const authorize = g.nodes.find((n) => n.id === 'authorize-payment');
  assert.equal(authorize.criteriaCount, 2);
  assert.equal(authorize.outputCount, 1);
});

test('a goto to a missing step is a problem, not an edge', () => {
  const doc = structuredClone(designerFixture);
  doc.workflows[0].steps[0].onFailure = [{ name: 'bad', type: 'goto', stepId: 'no-such-step' }];
  const g = projectWorkflow(doc, 'place-order');
  assert.ok(g.problems.some((p) => p.stepId === 'validate-order' && /no-such-step/.test(p.message)));
  assert.ok(!g.edges.some((e) => e.actionName === 'bad'));
});

test('an unresolvable reusable reference is a problem, not an action', () => {
  const doc = structuredClone(designerFixture);
  doc.workflows[0].steps[0].onFailure = [{ reference: '$components.failureActions.missing' }];
  const g = projectWorkflow(doc, 'place-order');
  assert.ok(g.problems.some((p) => /missing/.test(p.message)));
});

test('a step with no binding is a problem with kind unknown', () => {
  const doc = structuredClone(designerFixture);
  delete doc.workflows[0].steps[0].operationId;
  const g = projectWorkflow(doc, 'place-order');
  assert.equal(g.nodes[0].kind, 'unknown');
  assert.ok(g.problems.some((p) => p.stepId === 'validate-order' && /no operation/.test(p.message)));
});

test('an unknown workflowId projects empty with a problem', () => {
  const g = projectWorkflow(designerFixture, 'nope');
  assert.equal(g.nodes.length, 0);
  assert.equal(g.problems.length, 1);
});

test('a cross-workflow goto becomes an exit edge to a workflow: target', () => {
  const doc = structuredClone(designerFixture);
  doc.workflows[0].steps[0].onSuccess = [{ name: 'handoff', type: 'goto', workflowId: 'order-with-compensation' }];
  const g = projectWorkflow(doc, 'place-order');
  const exit = g.edges.find((e) => e.actionName === 'handoff');
  assert.equal(exit.to, 'workflow:order-with-compensation');
  assert.equal(exit.kind, 'success');
  // An unconditional goto also elides the sequence fall-through.
  assert.ok(!g.edges.some((e) => e.kind === 'seq' && e.from === 'validate-order'));
});
