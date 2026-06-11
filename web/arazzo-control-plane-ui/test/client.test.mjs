// Tier 1 — ArazzoControlPlaneClient behaviour against the in-memory mock. Pure Node, no DOM, no deps.
//   node --test test/client.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { ArazzoControlPlaneClient, ProblemError } from '../src/arazzo-client.js';
import { createMockControlPlane } from '../demo/mock-api.js';

function makeClient() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  return new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
}

test('listRuns returns all seeded runs', async () => {
  const { runs, nextPageToken } = await makeClient().listRuns({ limit: 100 });
  assert.equal(runs.length, 8);
  assert.equal(nextPageToken, null);
});

test('listRuns filters by status and workflowId', async () => {
  const c = makeClient();
  assert.equal((await c.listRuns({ status: 'Faulted' })).runs.length, 1);
  assert.equal((await c.listRuns({ workflowId: 'onboard' })).runs.length, 3);
});

test('listRunsPaged walks every page via the keyset token', async () => {
  let total = 0; let pages = 0;
  for await (const page of makeClient().listRunsPaged({ limit: 3 })) { total += page.runs.length; pages++; }
  assert.equal(total, 8);
  assert.equal(pages, 3);
});

test('getRun returns full detail and 404s for an unknown id', async () => {
  const c = makeClient();
  const run = await c.getRun('run-7f3a9c21');
  assert.equal(run.fault.stepId, 'reservePayment');
  assert.equal(run.cursor, 2);
  assert.ok(run.etag);
  await assert.rejects(() => c.getRun('nope'), (e) => e instanceof ProblemError && e.status === 404);
});

test('resumeRun clears the fault, and 409s when not faulted', async () => {
  const c = makeClient();
  const resumed = await c.resumeRun('run-7f3a9c21', { mode: 'RetryFaultedStep' });
  assert.equal(resumed.status, 'Running');
  assert.equal(resumed.fault, null);
  await assert.rejects(() => c.resumeRun('run-7f3a9c21', { mode: 'RetryFaultedStep' }), (e) => e.status === 409);
});

test('resumeRun rejects an unknown mode before calling the server', async () => {
  // resumeRun validates synchronously; an async wrapper turns the throw into a rejection for assert.rejects.
  await assert.rejects(async () => makeClient().resumeRun('x', { mode: 'Bogus' }), TypeError);
});

test('cancelRun cancels a non-terminal run, and 409s on a terminal one', async () => {
  const c = makeClient();
  assert.equal((await c.cancelRun('run-33aa71f9', { reason: 'test' })).status, 'Cancelled');
  await assert.rejects(() => c.cancelRun('run-0a5512cd'), (e) => e.status === 409);
});

test('deleteRun removes the run (then 404)', async () => {
  const c = makeClient();
  await c.deleteRun('run-2d77b410');
  await assert.rejects(() => c.getRun('run-2d77b410'), (e) => e.status === 404);
  await assert.rejects(() => c.deleteRun('run-2d77b410'), (e) => e.status === 404);
});

test('purgeRuns reaps old terminal runs', async () => {
  const c = makeClient();
  const cutoff = new Date(Date.now() - 5 * 86400000).toISOString();
  const before = (await c.listRuns({ limit: 100 })).runs.length;
  const { purgedCount } = await c.purgeRuns({ olderThan: cutoff });
  const after = (await c.listRuns({ limit: 100 })).runs.length;
  assert.ok(purgedCount >= 1);
  assert.equal(after, before - purgedCount);
});

test('listRuns time-window filters partition by createdAt with correct inclusivity', async () => {
  const c = makeClient();
  const all = (await c.listRuns({ limit: 100 })).runs;
  const dayAgo = new Date(Date.now() - 86400000).toISOString();
  const recent = (await c.listRuns({ createdAfter: dayAgo, limit: 100 })).runs;
  const old = (await c.listRuns({ createdBefore: dayAgo, limit: 100 })).runs;
  assert.equal(recent.length + old.length, all.length, 'after(inclusive) + before(exclusive) partitions the set');
  assert.ok(recent.every((r) => Date.parse(r.createdAt) >= Date.parse(dayAgo)));
  assert.ok(old.every((r) => Date.parse(r.createdAt) < Date.parse(dayAgo)));
});

test('listRuns updatedAfter narrows to recently-updated runs', async () => {
  const c = makeClient();
  const all = (await c.listRuns({ limit: 100 })).runs;
  const fiveMin = new Date(Date.now() - 5 * 60000).toISOString();
  const recent = (await c.listRuns({ updatedAfter: fiveMin, limit: 100 })).runs;
  assert.ok(recent.length >= 1 && recent.length < all.length);
  assert.ok(recent.every((r) => Date.parse(r.updatedAt) >= Date.parse(fiveMin)));
});

test('a problem+json error surfaces as ProblemError with status and title', async () => {
  const c = makeClient();
  await assert.rejects(
    () => c.getRun('missing'),
    (e) => e instanceof ProblemError && e.status === 404 && typeof e.title === 'string');
});
