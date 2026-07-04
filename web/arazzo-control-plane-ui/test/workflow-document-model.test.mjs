// Unit tests for the collaboration-ready document model (src/workflow-document-model.js):
// identity-addressed ops, inverse-op undo/redo, text round-trip producing the same minimal ops,
// and two-model convergence through a server-ordered relay.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { WorkflowDocumentModel, diff } from '../src/workflow-document-model.js';
import { designerFixture } from '../demo/designer-fixture.js';

const makeModel = (actor = 'a') => new WorkflowDocumentModel(designerFixture, { actor });

/** Wire two models through an in-order relay — the server-total-order assumption. */
function pair() {
  const a = makeModel('alice');
  const b = makeModel('bob');
  a.addEventListener('ops', (e) => b.applyRemote(e.detail));
  b.addEventListener('ops', (e) => a.applyRemote(e.detail));
  return [a, b];
}

test('edits reduce to identity-addressed ops, not index paths or snapshots', () => {
  const model = makeModel();
  let group;
  model.addEventListener('ops', (e) => { group = e.detail; });
  model.update((d) => {
    d.workflows[0].steps.find((s) => s.stepId === 'authorize-payment').description = 'NEW';
  }, { origin: 'inspector', label: 'edit description' });

  assert.equal(group.actor, 'a');
  assert.equal(group.ops.length, 1);
  const op = group.ops[0];
  assert.equal(op.kind, 'set');
  assert.deepEqual(op.path, ['workflows', { id: 'place-order' }, 'steps', { id: 'authorize-payment' }, 'description']);
  assert.equal(op.value, 'NEW');
  assert.equal(op.prev, 'Authorize the card for the order amount.');
});

test('a text-mode edit produces the SAME minimal op a structured editor would', () => {
  const model = makeModel();
  let group;
  model.addEventListener('ops', (e) => { group = e.detail; });
  const text = model.text.replace('"description": "Check the order is well-formed and in stock."', '"description": "VIA TEXT"');
  const result = model.applyText(text);
  assert.equal(result.ok, true);
  assert.equal(group.ops.length, 1);
  assert.deepEqual(group.ops[0].path, ['workflows', { id: 'place-order' }, 'steps', { id: 'validate-order' }, 'description']);
  // Broken JSON never touches the document.
  const before = model.text;
  assert.equal(model.applyText('{ nope').ok, false);
  assert.equal(model.text, before);
});

test('step reorder diffs to a move op, not wholesale list replacement', () => {
  const before = { workflows: [{ workflowId: 'w', steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c' }] }] };
  const after = { workflows: [{ workflowId: 'w', steps: [{ stepId: 'b' }, { stepId: 'a' }, { stepId: 'c' }] }] };
  const ops = diff(before, after);
  assert.deepEqual(ops, [{ kind: 'move', path: ['workflows', { id: 'w' }, 'steps'], from: 1, to: 0 }]);
});

test('undo/redo invert this actor’s own ops only; remote work survives a local undo', () => {
  const [alice, bob] = pair();
  alice.update((d) => { d.workflows[0].summary = 'ALICE'; }, { label: 'summary' });
  bob.update((d) => {
    d.workflows[0].steps.find((s) => s.stepId === 'validate-order').description = 'BOB';
  }, { label: 'description' });

  assert.equal(alice.document.workflows[0].summary, 'ALICE');
  assert.equal(alice.document.workflows[0].steps[0].description, 'BOB');

  alice.undo(); // reverts ALICE's summary — not BOB's edit
  assert.notEqual(alice.document.workflows[0].summary, 'ALICE');
  assert.equal(alice.document.workflows[0].steps[0].description, 'BOB', 'remote edit survives local undo');
  // The undo itself propagated as ops: both replicas agree.
  assert.equal(bob.text, alice.text);

  alice.redo();
  assert.equal(alice.document.workflows[0].summary, 'ALICE');
  assert.equal(bob.text, alice.text);
});

test('disjoint concurrent edits converge through the relay', () => {
  const [alice, bob] = pair();
  alice.update((d) => {
    d.workflows[0].steps.find((s) => s.stepId === 'authorize-payment').timeout = 1000;
  });
  bob.update((d) => {
    d.workflows[0].steps.find((s) => s.stepId === 'capture-payment').description = 'BOB WAS HERE';
  });
  assert.equal(alice.text, bob.text, 'replicas converge');
  assert.equal(alice.document.workflows[0].steps[1].timeout, 1000);
  assert.equal(alice.document.workflows[0].steps[3].description, 'BOB WAS HERE');
});

test('same-field concurrent writes resolve last-writer-wins in delivery order', () => {
  const [alice, bob] = pair();
  alice.update((d) => { d.workflows[0].summary = 'ALICE'; });
  bob.update((d) => { d.workflows[0].summary = 'BOB'; }); // delivered after — wins everywhere
  assert.equal(alice.document.workflows[0].summary, 'BOB');
  assert.equal(bob.document.workflows[0].summary, 'BOB');
});

test('ops whose target a remote actor deleted are skipped, not crashes', () => {
  const alice = makeModel('alice');
  const bob = makeModel('bob');
  let aliceGroup;
  alice.addEventListener('ops', (e) => { aliceGroup = e.detail; });

  // Alice edits a step; bob deletes that step before her ops arrive.
  alice.update((d) => {
    d.workflows[0].steps.find((s) => s.stepId === 'manual-review').description = 'EDITED';
  });
  bob.update((d) => {
    const wf = d.workflows[0];
    wf.steps = wf.steps.filter((s) => s.stepId !== 'manual-review');
    for (const s of wf.steps) delete s.onFailure; // drop dangling gotos for the test
  });
  const result = bob.applyRemote(aliceGroup);
  assert.equal(result.applied, 0);
  assert.equal(result.skipped, 1, 'the edit to the deleted step is skipped');
});

test('coalesced bursts form one undo unit; a new edit clears redo', () => {
  const model = makeModel();
  const original = model.document.workflows[0].summary;
  for (const s of ['o', 'on', 'one']) {
    model.update((d) => { d.workflows[0].summary = s; }, { label: 'type', coalesce: true });
  }
  model.undo();
  assert.equal(model.document.workflows[0].summary, original, 'one undo reverts the burst');
  model.update((d) => { d.workflows[0].summary = 'fresh'; });
  assert.ok(!model.canRedo);
});

test('no-op updates emit nothing', () => {
  const model = makeModel();
  let events = 0;
  model.addEventListener('ops', () => events++);
  model.update((d) => d, { label: 'noop' });
  assert.deepEqual(model.applyText(model.text), { ok: true, ops: [] });
  assert.equal(events, 0);
  assert.ok(!model.canUndo);
});

// ---- designer-state domain (layout rides the same stack and stream) ---------------------------

test('layout moves land on the SAME undo/redo stack as document edits, in gesture order', () => {
  const model = makeModel();
  model.updateDesignerState((s) => { s.nodes = { 'step-a': { x: 10, y: 20 } }; }, { label: 'move node' });
  model.update((d) => { d.workflows[0].summary = 'edited'; }, { label: 'edit summary' });
  model.updateDesignerState((s) => { s.nodes['step-a'] = { x: 99, y: 20 }; }, { label: 'move node' });

  model.undo(); // the second move
  assert.deepEqual(model.designerState.nodes['step-a'], { x: 10, y: 20 });
  assert.equal(model.document.workflows[0].summary, 'edited', 'the document edit is untouched');

  model.undo(); // the document edit
  assert.notEqual(model.document.workflows[0].summary, 'edited');
  assert.deepEqual(model.designerState.nodes['step-a'], { x: 10, y: 20 }, 'layout is untouched');

  model.undo(); // the first move — unpins the node entirely
  assert.equal(model.designerState.nodes?.['step-a'], undefined, 'undoing the first move unpins');

  model.redo();
  assert.deepEqual(model.designerState.nodes['step-a'], { x: 10, y: 20 }, 'redo re-pins');
});

test('designer-state ops never touch the document or its text form', () => {
  const model = makeModel();
  const textBefore = model.text;
  const result = model.updateDesignerState((s) => { s.nodes = { 'step-a': { x: 1, y: 2 } }; });
  assert.ok(result.ops.length > 0);
  assert.ok(result.ops.every((op) => op.d === 'designer'), 'ops are flagged with the designer domain');
  assert.equal(model.text, textBefore, 'the document text is byte-identical');
});

test('coalesced move bursts (drag/keyboard nudges) form one undo unit', () => {
  const model = makeModel();
  for (const x of [10, 20, 30]) {
    model.updateDesignerState((s) => { s.nodes = { 'step-a': { x, y: 0 } }; }, { label: 'move node', coalesce: true });
  }
  model.undo();
  assert.equal(model.designerState.nodes?.['step-a'], undefined, 'one undo reverts the whole burst');
});

test('layout ops ride the transport seam and converge on a collaborator model', () => {
  const a = makeModel('alice');
  const b = makeModel('bob');
  a.addEventListener('ops', (e) => b.applyRemote(e.detail));
  a.updateDesignerState((s) => { s.nodes = { 'step-a': { x: 5, y: 7 } }; }, { label: 'move node' });

  assert.deepEqual(b.designerState.nodes['step-a'], { x: 5, y: 7 }, 'the collaborator sees the move');
  assert.ok(!b.canUndo, 'a remote move never enters the local undo stack');
  assert.equal(b.text, a.text, 'documents stay identical');
});
