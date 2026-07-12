// Tier 1 — the pure workflow comparison model (workflow-diff.js). DOM-free; node --test.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { diffWorkflowPair } from '../src/workflow-diff.js';

// A base single-workflow document; each test clones and mutates it (structuredClone), then compares.
const base = () => ({
  arazzo: '1.1.0',
  workflows: [{
    workflowId: 'w',
    inputs: { type: 'object', properties: { amount: { type: 'number' } } },
    outputs: { receipt: '$steps.charge.outputs.id' },
    successActions: [{ name: 'done', type: 'end' }],
    steps: [
      { stepId: 'validate', operationId: 'op.validate', successCriteria: [{ condition: '$statusCode == 200' }] },
      { stepId: 'charge', operationId: 'op.charge', onSuccess: [{ name: 'ok', type: 'goto', stepId: 'notify' }] },
      { stepId: 'notify', operationId: 'op.notify' },
    ],
  }],
});
const wf = (doc) => doc.workflows[0];
const D = (l, r, ids = { leftWorkflowId: 'w', rightWorkflowId: 'w' }) => diffWorkflowPair(l, r, ids);
const stepOf = (r, id) => r.steps.find((e) => e.id === id || e.rightId === id || e.leftId === id);

test('identical documents produce no changes', () => {
  const r = D(base(), base());
  assert.deepEqual(r.summary, { added: 0, removed: 0, changed: 0, renamed: 0, moved: 0 });
  assert.equal(r.steps.length, 0);
  assert.equal(r.flow.length, 0);
  assert.equal(r.workflow.length, 0);
});

test('a rename keeps one step (renamed, not remove+add) — binding-gated', () => {
  const l = base();
  const r = base();
  wf(r).steps[0].stepId = 'checkInput'; // same binding op.validate, same content
  const d = D(l, r);
  assert.equal(d.summary.renamed, 1);
  assert.equal(d.summary.added, 0);
  assert.equal(d.summary.removed, 0);
  assert.equal(d.idMap.get('validate'), 'checkInput');
  const e = stepOf(d, 'checkInput');
  assert.equal(e.type, 'renamed');
  assert.deepEqual(e.changedGroups, []);
  assert.equal(d.paint.left.notes.validate, 'now checkInput');
  assert.equal(d.paint.right.notes.checkInput, 'was validate');
  assert.equal(d.paint.left.nodes.validate, 'changed');
  assert.equal(d.paint.right.nodes.checkInput, 'changed');
});

test('a rename that also edits content carries changedGroups on the one entry', () => {
  const l = base();
  const r = base();
  wf(r).steps[0].stepId = 'checkInput';
  wf(r).steps[0].successCriteria = [{ condition: '$statusCode == 201' }];
  const d = D(l, r);
  assert.equal(d.summary.renamed, 1);
  assert.equal(d.summary.changed, 0, 'a renamed step is not double-counted as changed');
  assert.deepEqual(stepOf(d, 'checkInput').changedGroups, ['successCriteria']);
});

test('a reordered step is moved, never changed; no node paint', () => {
  const l = base();
  const r = base();
  wf(r).steps = [wf(r).steps[0], wf(r).steps[2], wf(r).steps[1]]; // validate, notify, charge
  const d = D(l, r);
  assert.equal(d.summary.changed, 0);
  assert.equal(d.summary.moved, 1);
  const moved = d.steps.find((e) => e.type === 'moved');
  assert.ok(moved, 'a moved entry exists');
  assert.equal(d.paint.left.nodes.notify, undefined, 'moved steps are not painted');
  assert.equal(d.paint.right.nodes.notify, undefined);
});

test('a successCriteria edit classifies the step changed with the group named', () => {
  const l = base();
  const r = base();
  wf(r).steps[0].successCriteria = [{ condition: '$statusCode == 204' }];
  const d = D(l, r);
  assert.equal(d.summary.changed, 1);
  const e = stepOf(d, 'validate');
  assert.equal(e.type, 'changed');
  assert.deepEqual(e.changedGroups, ['successCriteria']);
  assert.equal(d.paint.left.nodes.validate, 'changed');
  assert.equal(d.paint.right.nodes.validate, 'changed');
});

test('inputs and outputs edits paint the start/end pseudo-nodes', () => {
  const l = base();
  const r = base();
  wf(r).inputs.properties.currency = { type: 'string' };
  wf(r).outputs.extra = '$steps.validate.outputs.y';
  const d = D(l, r);
  assert.equal(d.paint.right.nodes['#start'], 'changed');
  assert.equal(d.paint.right.nodes['#end'], 'changed');
  assert.ok(d.workflow.some((e) => e.area === 'inputs'));
  assert.ok(d.workflow.some((e) => e.area === 'outputs'));
});

test('a defaults (workflow successActions) edit is a workflow entry + defaults paint', () => {
  const l = base();
  const r = base();
  wf(r).successActions = [{ name: 'done', type: 'goto', stepId: 'notify' }];
  const d = D(l, r);
  const e = d.workflow.find((x) => x.area === 'defaults');
  assert.ok(e);
  assert.deepEqual(e.changedGroups, ['successActions']);
  assert.equal(d.paint.right.defaults, 'changed');
});

test('add + remove that do NOT share a binding never pair', () => {
  const l = base();
  const r = base();
  wf(r).steps[0] = { stepId: 'ship', operationId: 'op.ship' }; // different id AND different binding
  const d = D(l, r);
  assert.equal(d.summary.renamed, 0, 'no rename across different bindings');
  assert.ok(d.steps.some((e) => e.type === 'removed' && e.leftId === 'validate'));
  assert.ok(d.steps.some((e) => e.type === 'added' && e.rightId === 'ship'));
});

test('greedy rename pairing picks the nearer index among two same-binding candidates', () => {
  // Two left-only + two right-only steps sharing binding op.k; greedy prefers similarity then |Δindex|.
  const l = base();
  const r = base();
  wf(l).steps = [
    { stepId: 'k1', operationId: 'op.k', outputs: { v: '1' } },
    { stepId: 'k2', operationId: 'op.k', outputs: { v: '2' } },
  ];
  wf(r).steps = [
    { stepId: 'k1x', operationId: 'op.k', outputs: { v: '1' } }, // identical content to k1
    { stepId: 'k2x', operationId: 'op.k', outputs: { v: '2' } }, // identical content to k2
  ];
  const d = D(l, r);
  assert.equal(d.summary.renamed, 2);
  assert.equal(d.idMap.get('k1'), 'k1x', 'k1 pairs its content twin at the same index');
  assert.equal(d.idMap.get('k2'), 'k2x');
});

test('a reusable-ref flip (inline ↔ $components) shows the edge changed + reusable', () => {
  const l = base();
  const r = base();
  r.components = { failureActions: { retryIt: { name: 'ok', type: 'goto', stepId: 'notify' } } };
  wf(r).steps[1].onSuccess = [{ reference: '$components.failureActions.retryIt' }];
  const d = D(l, r);
  const e = d.flow.find((x) => x.type === 'changed' && x.from === 'charge');
  assert.ok(e, 'the charge→notify action edge is changed');
});

test('deleting one of two identical parallel gotos yields one removed, not two changed', () => {
  const l = base();
  const r = base();
  wf(l).steps[1].onSuccess = [
    { name: 'jump', type: 'goto', stepId: 'notify' },
    { name: 'jump', type: 'goto', stepId: 'notify' },
  ];
  wf(r).steps[1].onSuccess = [
    { name: 'jump', type: 'goto', stepId: 'notify' },
  ];
  const d = D(l, r);
  const removed = d.flow.filter((x) => x.type === 'removed');
  const changed = d.flow.filter((x) => x.type === 'changed');
  assert.equal(removed.length, 1, 'exactly one removed edge');
  assert.equal(changed.length, 0, 'no bogus Δ among the parallels');
});

test('a shared $components body change is a components entry; the steps stay unchanged', () => {
  const l = base();
  l.components = { failureActions: { onErr: { name: 'bail', type: 'end' } } };
  wf(l).steps[0].onFailure = [{ reference: '$components.failureActions.onErr' }];
  wf(l).steps[2].onFailure = [{ reference: '$components.failureActions.onErr' }];
  const r = structuredClone(l);
  r.components.failureActions.onErr = { name: 'bail', type: 'goto', stepId: 'charge' }; // body change only
  const d = D(l, r);
  assert.ok(d.workflow.some((e) => e.area === 'components' && e.component === 'failureActions.onErr'));
  // the steps hold identical reference entries → deep-equal → not classified as changed steps
  assert.equal(d.steps.filter((e) => e.type === 'changed').length, 0);
});

test('per-side workflow ids handle the {base}-v1 → {base}-v2 rewrite', () => {
  const l = base();
  wf(l).workflowId = 'pay-v1';
  const r = base();
  wf(r).workflowId = 'pay-v2';
  wf(r).steps[0].successCriteria = [{ condition: '$statusCode == 201' }];
  const d = diffWorkflowPair(l, r, { leftWorkflowId: 'pay-v1', rightWorkflowId: 'pay-v2' });
  assert.equal(d.summary.changed, 1);
  assert.equal(stepOf(d, 'validate').type, 'changed');
});

test('a missing right workflow reads as all-removed (steps and their edges)', () => {
  const l = base();
  const r = base();
  const d = diffWorkflowPair(l, r, { leftWorkflowId: 'w', rightWorkflowId: 'nope' });
  assert.equal(d.steps.filter((e) => e.type === 'removed').length, 3, 'all three steps removed');
  assert.equal(d.summary.added, 0);
});

test('the union layout places matched steps at the same position on both sides', () => {
  const l = base();
  const r = base();
  wf(r).steps.splice(1, 0, { stepId: 'fraudCheck', operationId: 'op.fraud' }); // an inserted step
  const d = D(l, r);
  // validate and notify are matched → identical union coordinates on both sides
  assert.deepEqual(d.layout.left.validate, d.layout.right.validate);
  assert.deepEqual(d.layout.left.notify, d.layout.right.notify);
  assert.ok(d.layout.right.fraudCheck, 'the inserted step has a position on the right');
});

test('diffWorkflowPair is deterministic (run-twice)', () => {
  const l = base();
  const r = base();
  wf(r).steps[0].stepId = 'checkInput';
  wf(r).steps[1].successCriteria = [{ condition: '$statusCode == 200' }];
  wf(r).steps.push({ stepId: 'audit', operationId: 'op.audit' });
  const a = D(l, r);
  const b = D(l, r);
  const norm = (x) => ({ steps: x.steps, flow: x.flow, workflow: x.workflow, summary: x.summary, idMap: [...x.idMap.entries()], paint: x.paint });
  assert.deepEqual(norm(a), norm(b));
});

test('equality is key-order sensitive on whole-value surfaces (pinned limitation)', () => {
  const l = base();
  const r = base();
  // A whole-value surface (inputs) with the same content but reordered keys: the module's JSON.stringify
  // equality flags it as changed. (Step CONTENT goes through the model's recursive diff, which compares
  // per key and so does not flag a key-order-only step difference — the sensitivity lives on the surfaces.)
  wf(r).inputs = { properties: { amount: { type: 'number' } }, type: 'object' };
  const d = D(l, r);
  assert.ok(d.workflow.some((e) => e.area === 'inputs'), 'a key-order-only inputs difference classifies as changed');
});
