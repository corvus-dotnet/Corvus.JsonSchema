// Tier 1 — ArazzoControlPlaneClient behaviour against the in-memory mock. Pure Node, no DOM, no deps.
//   node --test test/client.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { ArazzoControlPlaneClient, ProblemError } from '../src/arazzo-client.js';
import { packWorkflowPackage } from '../src/workflow-package.js';
import { createMockControlPlane } from '../demo/mock-api.js';

function workflowJson(workflowId, title, sources = []) {
  return JSON.stringify({
    arazzo: '1.1.0',
    info: { title, description: `${title} — workflow.` },
    sourceDescriptions: sources.map((s) => ({ name: s.name, type: s.type || 'openapi' })),
    workflows: [{ workflowId, steps: [] }],
  });
}

function makeClient() {
  const mock = createMockControlPlane({ latencyMs: 0 });
  return new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
}

test('listRuns returns all seeded runs', async () => {
  const { runs, nextPageToken } = await makeClient().listRuns({ limit: 100 });
  assert.equal(runs.length, 12);
  assert.equal(nextPageToken, null);
});

test('listRuns filters by status and workflowId', async () => {
  const c = makeClient();
  assert.equal((await c.listRuns({ status: 'Faulted' })).runs.length, 5);
  assert.equal((await c.listRuns({ workflowId: 'onboard' })).runs.length, 5);
});

test('listRunsPaged walks every page via the keyset token', async () => {
  let total = 0; let pages = 0;
  for await (const page of makeClient().listRunsPaged({ limit: 3 })) { total += page.runs.length; pages++; }
  assert.equal(total, 12);
  assert.equal(pages, 4);
});

test('getRun returns full detail and 404s for an unknown id', async () => {
  const c = makeClient();
  const run = await c.getRun('run-7f3a9c21');
  assert.equal(run.fault.stepId, 'reservePayment');
  assert.equal(run.cursor, 1);
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

// ---- catalog ------------------------------------------------------------------------------------

test('searchCatalog returns all seeded versions', async () => {
  const { versions, nextPageToken } = await makeClient().searchCatalog({ limit: 100 });
  assert.equal(versions.length, 5);
  assert.equal(nextPageToken, null);
});

test('searchCatalog filters by status, tag, owner and free-text q', async () => {
  const c = makeClient();
  assert.equal((await c.searchCatalog({ status: 'Obsolete' })).versions.length, 1);
  assert.equal((await c.searchCatalog({ status: 'Active' })).versions.length, 4);
  assert.equal((await c.searchCatalog({ tags: ['beta'] })).versions.length, 1);
  assert.equal((await c.searchCatalog({ tags: ['prod', 'billing'] })).versions.length, 3);
  assert.equal((await c.searchCatalog({ owner: 'onboarding' })).versions.length, 2);
  assert.equal((await c.searchCatalog({ q: 'adopt' })).versions.length, 1);
});

test('searchCatalog filters by workflowIdPrefix (case-insensitive, anchored)', async () => {
  const c = makeClient();
  assert.equal((await c.searchCatalog({ workflowIdPrefix: 'nightly' })).versions.length, 3, 'base-name prefix matches all versions');
  assert.equal((await c.searchCatalog({ workflowIdPrefix: 'ADOPT-PET' })).versions.length, 1, 'case-insensitive');
  assert.equal((await c.searchCatalog({ workflowIdPrefix: 'nightly-reconcile-v3' })).versions.length, 1, 'versioned-id prefix narrows to one');
  assert.equal((await c.searchCatalog({ workflowIdPrefix: 'zzz' })).versions.length, 0, 'no match');
});

test('searchCatalog filters by baseWorkflowId', async () => {
  const { versions } = await makeClient().searchCatalog({ baseWorkflowId: 'nightly-reconcile' });
  assert.equal(versions.length, 3);
  assert.ok(versions.every((v) => v.baseWorkflowId === 'nightly-reconcile'));
});

test('validateCatalogValue reports conformance against the version schema', async () => {
  const c = makeClient();
  const target = { kind: 'stepOutputs', workflowId: 'adopt-pet-v1', stepId: 'reservePayment' };

  const ok = await c.validateCatalogValue('adopt-pet', 1, target, { amount: 5, status: 'settled' });
  assert.equal(ok.valid, true);
  assert.equal(ok.errors.length, 0);

  const bad = await c.validateCatalogValue('adopt-pet', 1, target, { amount: -5, status: 'nope' });
  assert.equal(bad.valid, false);
  assert.ok(bad.errors.length >= 1, 'reports the failing constraints');
});

test('validateCatalogValue handles unions, tuples and maps', async () => {
  const c = makeClient();

  // Union (oneOf with a discriminator): a document variant needs its document fields.
  const evidence = (value) => c.validateCatalogValue('onboard-customer', 1, { kind: 'stepOutputs', workflowId: 'onboard-customer-v1', stepId: 'verifyIdentity' }, value);
  assert.equal((await evidence({ evidence: { kind: 'document', documentType: 'passport', documentNumber: 'X1' } })).valid, true);
  assert.equal((await evidence({ evidence: { kind: 'document' } })).valid, false); // missing required variant fields
  assert.equal((await evidence({ score: 5 })).valid, false); // score exceeds maximum 1

  // Tuple (prefixItems): each position is an integer >= 0.
  const range = (value) => c.validateCatalogValue('nightly-reconcile', 3, { kind: 'stepOutputs', workflowId: 'nightly-reconcile-v3', stepId: 'flagDiscrepancies' }, value);
  assert.equal((await range({ range: [0, 10] })).valid, true);
  assert.equal((await range({ range: [-1, 10] })).valid, false);

  // Map (additionalProperties): values must be strings.
  const tags = (value) => c.validateCatalogValue('onboard-customer', 1, { kind: 'stepOutputs', workflowId: 'onboard-customer-v1', stepId: 'provisionResources' }, value);
  assert.equal((await tags({ tags: { env: 'prod' } })).valid, true);
  assert.equal((await tags({ tags: { env: 5 } })).valid, false);
});

test('searchCatalogPaged walks every page via the keyset token', async () => {
  let total = 0; let pages = 0;
  for await (const page of makeClient().searchCatalogPaged({ limit: 2 })) { total += page.versions.length; pages++; }
  assert.equal(total, 5);
  assert.equal(pages, 3);
});

test('listCatalogVersions returns the versions of one base id, ordered', async () => {
  const { versions } = await makeClient().listCatalogVersions('nightly-reconcile');
  assert.deepEqual(versions.map((v) => v.versionNumber), [1, 2, 3]);
});

test('getCatalogVersion returns metadata and 404s for an unknown version', async () => {
  const c = makeClient();
  const v = await c.getCatalogVersion('nightly-reconcile', 3);
  assert.equal(v.status, 'Active');
  assert.deepEqual(v.tags, ['prod', 'billing', 'beta']);
  assert.equal(v.owner.email, 'reconcile@example.com');
  await assert.rejects(() => c.getCatalogVersion('nightly-reconcile', 99), (e) => e instanceof ProblemError && e.status === 404);
});

test('getCatalogPackage returns the archive as a Blob', async () => {
  const blob = await makeClient().getCatalogPackage('adopt-pet', 1);
  assert.ok(blob instanceof Blob);
  assert.ok(blob.size > 0);
});

test('getCatalogWorkflowSchemas returns typed inputs and resolved step outputs', async () => {
  const c = makeClient();
  const schemas = await c.getCatalogWorkflowSchemas('adopt-pet', 1);
  const wf = schemas.workflows['adopt-pet-v1'];
  assert.ok(wf, 'keyed by the versioned workflow id');
  const reserve = wf.steps.reservePayment.outputs;
  assert.equal(reserve.paymentId.format, 'uuid', 'string format recognised');
  assert.equal(reserve.status.enum.length, 3, 'enum carried through for a dropdown');
  assert.equal(reserve.amount.type, 'number');
});

test('getCatalogWorkflow and getCatalogSource return documents (and 404 for an unknown source)', async () => {
  const c = makeClient();
  const wf = await c.getCatalogWorkflow('adopt-pet', 1);
  assert.equal(wf.workflows[0].workflowId, 'adopt-pet-v1');
  const src = await c.getCatalogSource('adopt-pet', 1, 'petstore');
  assert.equal(src.openapi, '3.1.0');
  await assert.rejects(() => c.getCatalogSource('adopt-pet', 1, 'nope'), (e) => e.status === 404);
});

test('updateCatalogVersion patches governance metadata', async () => {
  const c = makeClient();
  const updated = await c.updateCatalogVersion('adopt-pet', 1, { tags: ['prod', 'reviewed'] });
  assert.deepEqual(updated.tags, ['prod', 'reviewed']);
  assert.ok(updated.lastUpdatedAt);
});

test('obsoleteCatalogVersion flips status to Obsolete with audit fields', async () => {
  const c = makeClient();
  const v = await c.obsoleteCatalogVersion('nightly-reconcile', 2);
  assert.equal(v.status, 'Obsolete');
  assert.ok(v.obsoletedBy);
  assert.ok(v.obsoletedAt);
});

test('addCatalogVersion uploads a package as multipart and returns the new summary', async () => {
  const c = makeClient();
  const pkg = new Blob([new TextEncoder().encode('fake-zip-bytes')], { type: 'application/octet-stream' });
  const added = await c.addCatalogVersion({ package: pkg, owner: { name: 'Me', email: 'me@example.com' }, tags: ['draft'] });
  assert.equal(added.status, 'Active');
  assert.deepEqual(added.tags, ['draft']);
  assert.equal((await c.searchCatalog({ limit: 100 })).versions.length, 6);
});

test('addCatalogVersion rejects a missing package or owner before calling the server', async () => {
  await assert.rejects(async () => makeClient().addCatalogVersion({ owner: { name: 'x', email: 'y' } }), TypeError);
  await assert.rejects(async () => makeClient().addCatalogVersion({ package: new Blob(['x']) }), TypeError);
});

test('packWorkflowPackage builds an archive the catalog versions by base id (auto-versioning)', async () => {
  const c = makeClient();
  // The seed already has nightly-reconcile v1/v2/v3 → an upload becomes v4 for that base.
  const pkg = packWorkflowPackage(
    workflowJson('nightly-reconcile', 'Nightly Reconcile', [{ name: 'petstore' }]),
    [{ name: 'petstore', content: JSON.stringify({ openapi: '3.1.0', info: { title: 'Petstore', version: '1.0.0' } }) }],
  );
  const added = await c.addCatalogVersion({ package: pkg, owner: { name: 'Me', email: 'me@example.com' }, tags: ['prod'] });
  assert.equal(added.baseWorkflowId, 'nightly-reconcile');
  assert.equal(added.versionNumber, 4, 'continues the existing version line');
  assert.equal(added.workflowId, 'nightly-reconcile-v4', 'workflow id rewritten with the assigned version');
  assert.deepEqual(added.sources.map((s) => s.name), ['petstore'], 'sources projected from the package');

  // A brand-new base id starts at v1.
  const fresh = await c.addCatalogVersion({
    package: packWorkflowPackage(workflowJson('brand-new', 'Brand New'), []),
    owner: { name: 'Me', email: 'me@example.com' },
  });
  assert.equal(fresh.workflowId, 'brand-new-v1');
});

test('addCatalogVersion is rejected (400) when the package workflow id already carries -vN', async () => {
  const c = makeClient();
  const pkg = packWorkflowPackage(workflowJson('thing-v2', 'Thing'), []);
  await assert.rejects(
    () => c.addCatalogVersion({ package: pkg, owner: { name: 'a', email: 'b@example.com' } }),
    (e) => e instanceof ProblemError && e.status === 400);
});

test('deleteCatalogVersion removes an unreferenced version (then 404)', async () => {
  const c = makeClient();
  // nightly-reconcile v1 is Obsolete and has no referencing runs in the seed.
  await c.deleteCatalogVersion('nightly-reconcile', 1);
  await assert.rejects(() => c.getCatalogVersion('nightly-reconcile', 1), (e) => e.status === 404);
});

test('deleteCatalogVersion is refused (409) while runs reference the version', async () => {
  const c = makeClient();
  // adopt-pet v1 is referenced by seeded runs (workflowId adopt-pet-v1).
  await assert.rejects(
    () => c.deleteCatalogVersion('adopt-pet', 1),
    (e) => e instanceof ProblemError && e.status === 409);
});

test('purgeCatalog reaps obsolete, unreferenced versions', async () => {
  const c = makeClient();
  const before = (await c.searchCatalog({ limit: 100 })).versions.length;
  const { purgedCount } = await c.purgeCatalog();
  const after = (await c.searchCatalog({ limit: 100 })).versions.length;
  assert.ok(purgedCount >= 1);
  assert.equal(after, before - purgedCount);
});
