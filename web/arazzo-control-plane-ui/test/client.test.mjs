// Tier 1 — ArazzoControlPlaneClient behaviour against the in-memory mock. Pure Node, no DOM, no deps.
//   node --test test/client.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { ArazzoControlPlaneClient, ProblemError } from '../src/arazzo-client.js';
import { packWorkflowPackage, unpackWorkflowPackage } from '../src/workflow-package.js';
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
  // Free-text q is a substring over title/description AND the workflow id — so "pet" finds adopt-pet (id contains it),
  // not just an anchored id prefix. This is what the workflow picker relies on.
  assert.equal((await c.searchCatalog({ q: 'pet' })).versions.length, 1, 'q matches the id substring (adopt-pet)');
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
  // The discriminator pins the variant, so errors are the document variant's own fields, not a blanket "no match".
  const docErrors = (await evidence({ evidence: { kind: 'document' } })).errors;
  assert.ok(docErrors.some((e) => e.instancePath === '/evidence/documentType'), 'reports the chosen variant\'s field');
  assert.ok(!docErrors.some((e) => /any allowed type/.test(e.message)), 'no blanket union failure');
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

test('addCatalogVersion carries securityTags and rejects the reserved sys: prefix (400)', async () => {
  const c = makeClient();
  const pkg = packWorkflowPackage(workflowJson('sec-flow', 'Sec Flow'), []);
  const added = await c.addCatalogVersion({ package: pkg, owner: { name: 'Me', email: 'me@example.com' }, securityTags: [{ key: 'domain', value: 'payments' }] });
  assert.deepEqual(added.securityTags, [{ key: 'domain', value: 'payments' }]);

  const pkg2 = packWorkflowPackage(workflowJson('sec-flow-2', 'Sec Flow 2'), []);
  await assert.rejects(
    () => c.addCatalogVersion({ package: pkg2, owner: { name: 'Me', email: 'me@example.com' }, securityTags: [{ key: 'sys:tenant', value: 'evil' }] }),
    (e) => e instanceof ProblemError && e.status === 400);
});

test('updateCatalogVersion re-tags securityTags and rejects the reserved sys: prefix (400)', async () => {
  const c = makeClient();
  const updated = await c.updateCatalogVersion('adopt-pet', 1, { securityTags: [{ key: 'domain', value: 'pets' }, { key: 'team', value: 'shelter' }] });
  assert.deepEqual(updated.securityTags, [{ key: 'domain', value: 'pets' }, { key: 'team', value: 'shelter' }]);
  await assert.rejects(
    () => c.updateCatalogVersion('adopt-pet', 1, { securityTags: [{ key: 'sys:tenant', value: 'evil' }] }),
    (e) => e instanceof ProblemError && e.status === 400);
});

test('unpackWorkflowPackage round-trips the AWP container packWorkflowPackage builds', async () => {
  const wf = workflowJson('round-trip', 'Round Trip', [{ name: 'petstore' }]);
  const src = JSON.stringify({ openapi: '3.1.0', info: { title: 'Petstore', version: '1.0.0' } });
  const blob = packWorkflowPackage(wf, [{ name: 'petstore', content: src }]);
  const entries = unpackWorkflowPackage(await blob.arrayBuffer());
  assert.deepEqual([...entries.keys()].sort(), ['sources/petstore.json', 'workflow.json']);
  assert.equal(entries.get('workflow.json'), wf);
  assert.equal(entries.get('sources/petstore.json'), src);
  assert.throws(() => unpackWorkflowPackage(new TextEncoder().encode('not a package')), /AWP/);
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

// ---- credentials (§13) — references and non-secret metadata only --------------------------------

test('listCredentials returns the seeded bindings with a derived credentialStatus', async () => {
  const { credentials } = await makeClient().listCredentials();
  assert.equal(credentials.length, 8);
  const byKey = Object.fromEntries(credentials.map((c) => [`${c.sourceName}@${c.environment}`, c.credentialStatus]));
  assert.equal(byKey['petstore@production'], 'valid');      // 20 days out
  assert.equal(byKey['billing@production'], 'expiringSoon'); // 3 days out (inside the 7-day window)
  assert.equal(byKey['legacy@production'], 'expired');       // 2 days past
  assert.equal(byKey['events@staging'], 'valid');            // no expiry
  // references only — every secretRef carries a scheme, never inline secret material.
  assert.ok(credentials.every((c) => c.secretRefs.every((r) => /:\/\//.test(r.ref))));
});

test('listCredentials keyset-pages: limit + the opaque nextPageToken walk every binding once, in order', async () => {
  const c = makeClient();
  const first = await c.listCredentials({ limit: 2 });
  assert.equal(first.credentials.length, 2, 'a page respects the limit');
  assert.ok(first.nextPageToken, 'a next-page token when more remain');

  // Walk every page via the iterator and assert no gaps/duplicates and (sourceName, environment) order.
  const seen = [];
  for await (const page of c.listCredentialsPaged({ limit: 2 })) {
    assert.ok(page.credentials.length <= 2, 'each page respects the limit');
    for (const b of page.credentials) seen.push(`${b.sourceName}@${b.environment}`);
  }
  assert.equal(seen.length, 8, 'all eight bindings, exactly once');
  assert.equal(new Set(seen).size, 8, 'no duplicates across page boundaries');
  assert.deepEqual(seen, [...seen].sort(), 'ordered by sourceName then environment');
});

test('getCredential returns one binding and 404s for an unknown key', async () => {
  const c = makeClient();
  const b = await c.getCredential('petstore', 'production');
  assert.equal(b.sourceName, 'petstore');
  assert.equal(b.secretRefs[0].ref, 'keyvault://petstore-key#3');
  await assert.rejects(() => c.getCredential('nope', 'production'), (e) => e instanceof ProblemError && e.status === 404);
});

test('createCredential adds a binding, rejects an inline secret (400) and a duplicate (409)', async () => {
  const c = makeClient();
  const created = await c.createCredential({ sourceName: 'new', environment: 'production', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'keyvault://new-key' }] });
  assert.equal(created.sourceName, 'new');
  assert.equal(created.credentialStatus, 'valid');
  // an inline secret (no scheme) is refused at the edge — secret material cannot be persisted.
  await assert.rejects(
    () => c.createCredential({ sourceName: 'x', environment: 'y', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'hunter2' }] }),
    (e) => e instanceof ProblemError && e.status === 400);
  // a duplicate (sourceName, environment) conflicts.
  await assert.rejects(
    () => c.createCredential({ sourceName: 'petstore', environment: 'production', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'env://X' }] }),
    (e) => e.status === 409);
});

test('updateCredential re-points the reference (rotation) and 404s for an unknown key', async () => {
  const c = makeClient();
  const updated = await c.updateCredential('petstore', 'production', {
    authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'keyvault://petstore-key#4' }], rotatedAt: new Date().toISOString(),
  });
  assert.equal(updated.secretRefs[0].ref, 'keyvault://petstore-key#4');
  assert.ok(updated.rotatedAt);
  await assert.rejects(
    () => c.updateCredential('nope', 'production', { authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'env://X' }] }),
    (e) => e.status === 404);
});

test('deleteCredential removes the binding (then 404)', async () => {
  const c = makeClient();
  await c.deleteCredential('events', 'staging');
  await assert.rejects(() => c.getCredential('events', 'staging'), (e) => e.status === 404);
});

// ---- administrators (§15) — resolved identities {digest, identity:[{dimension,value}], kind?, label?} ----------

test('listAdministrators returns resolved-identity grants, and an empty set for an unknown base id', async () => {
  const c = makeClient();
  const { administrators } = await c.listAdministrators('nightly-reconcile');
  assert.equal(administrators.length, 3); // Platform team + alice@ops (seeded on every workflow) + Ada Lovelace (§6.1 Access Overview seed)
  assert.ok(administrators.some((a) => a.kind === 'team' && a.identity[0].value === 'platform'));
  assert.ok(administrators.some((a) => a.identity.some((t) => t.dimension === 'sys:sub' && t.value === 'alice@ops')));
  assert.match(administrators[0].digest, /^[0-9a-f]{64}$/);
  assert.deepEqual((await c.listAdministrators('ghost')).administrators, []);
});

test('addAdministrator is idempotent and transferAdministration replaces the whole set', async () => {
  const c = makeClient();
  const added = await c.addAdministrator('nightly-reconcile', { dimension: 'tenant', value: 'growth' });
  assert.equal(added.administrators.length, 4); // Platform + alice@ops + Ada (seeded) + the newly-added Growth
  const again = await c.addAdministrator('nightly-reconcile', { dimension: 'tenant', value: 'growth' });
  assert.equal(again.administrators.length, 4, 'idempotent');
  const transferred = await c.transferAdministration('nightly-reconcile', { administrators: [{ dimension: 'tenant', value: 'acme' }] });
  assert.equal(transferred.administrators.length, 1);
  assert.deepEqual(transferred.administrators[0].identity, [{ dimension: 'tenant', value: 'acme' }]);
});

test('addAdministrator accepts a resolved grantee (kind + value + identity)', async () => {
  const c = makeClient();
  const added = await c.addAdministrator('nightly-reconcile', {
    kind: 'person', value: 'u-1042', label: 'Ada Lovelace',
    identity: [{ dimension: 'sys:iss', value: 'https://idp.example.com' }, { dimension: 'sys:sub', value: 'u-1042' }],
    complete: true,
  });
  // nightly-reconcile is also seeded with a single-dimension Ada (sys:sub only, §6.1); disambiguate by the two-dimension
  // identity this resolved grantee (sys:iss + sys:sub) was added with.
  const ada = added.administrators.find((a) => a.label === 'Ada Lovelace' && a.identity.length === 2);
  assert.ok(ada, 'the resolved grantee was added');
  assert.equal(ada.kind, 'person');
  assert.equal(ada.identity.length, 2);
});

test('removeAdministrator (by identity digest) refuses to remove the last administrator (409)', async () => {
  const c = makeClient();
  // adopt-pet has a single administrator (alice@ops) — removing it is refused.
  const [sole] = (await c.listAdministrators('adopt-pet')).administrators;
  await assert.rejects(
    () => c.removeAdministrator('adopt-pet', sole.digest),
    (e) => e instanceof ProblemError && e.status === 409);
  // onboard-customer has several — removing one succeeds.
  const { administrators: onboard } = await c.listAdministrators('onboard-customer');
  const after = await c.removeAdministrator('onboard-customer', onboard[onboard.length - 1].digest);
  assert.equal(after.administrators.length, onboard.length - 1);
});

test('addAdministrator / transferAdministration validate before calling the server', async () => {
  await assert.rejects(async () => makeClient().addAdministrator('x', { dimension: 'tenant' }), TypeError);
  await assert.rejects(async () => makeClient().transferAdministration('x', { administrators: [] }), TypeError);
});

// ---- security rules (§14.2) ---------------------------------------------------------------------

test('security rules: list, create, update and delete round-trip through the store', async () => {
  const c = makeClient();
  const { rules } = await c.searchSecurityRules();
  assert.ok(rules.some((r) => r.name === 'tenant-scoped' && r.expression === 'tenant == $claim.tenant'));
  assert.ok(rules.some((r) => r.name === 'reach-core-tenants' && r.expression === "tenant in ('acme', 'globex')"));

  const created = await c.createSecurityRule({ name: 'reach-eu', expression: "region == 'eu'", description: 'EU rows.' });
  assert.equal(created.name, 'reach-eu');
  assert.equal(created.expression, "region == 'eu'");
  assert.ok((await c.searchSecurityRules()).rules.some((r) => r.name === 'reach-eu'));

  const updated = await c.updateSecurityRule('reach-eu', { expression: "region in ('eu', 'uk')" });
  assert.equal(updated.expression, "region in ('eu', 'uk')");

  await c.deleteSecurityRule('reach-eu');
  assert.ok(!(await c.searchSecurityRules()).rules.some((r) => r.name === 'reach-eu'));
});

test('createSecurityRule rejects a malformed expression (400) and a duplicate name (409)', async () => {
  const c = makeClient();
  await assert.rejects(
    () => c.createSecurityRule({ name: 'bad', expression: '(tenant == ' }),
    (e) => e instanceof ProblemError && e.status === 400);
  await assert.rejects(
    () => c.createSecurityRule({ name: 'tenant-scoped', expression: "tenant == 'x'" }),
    (e) => e instanceof ProblemError && e.status === 409);
});

test('security-rule client methods validate their arguments before calling the server', async () => {
  const c = makeClient();
  await assert.rejects(async () => c.createSecurityRule({ name: 'x' }), TypeError);
  await assert.rejects(async () => c.createSecurityRule({ expression: 'x' }), TypeError);
  await assert.rejects(async () => c.updateSecurityRule('x', {}), TypeError);
  await assert.rejects(async () => c.deleteSecurityRule(''), TypeError);
});

test('listSecurityOrderings returns the configured ordered dimensions with their ascending labels', async () => {
  const { orderings } = await makeClient().listSecurityOrderings();
  const classification = orderings.find((o) => o.dimension === 'classification');
  assert.ok(classification, 'classification ordering present');
  assert.deepEqual(classification.labels, ['public', 'internal', 'confidential', 'restricted']);
});

test('security bindings: list, create, update and delete round-trip through the store', async () => {
  const c = makeClient();
  const { bindings } = await c.searchSecurityBindings();
  assert.ok(bindings.some((b) => b.claimType === 'team' && b.claimValue === 'payments'));

  const created = await c.createSecurityBinding({ claimType: 'role', claimValue: 'sre', read: { unrestricted: true }, write: { ruleNames: ['reach-payments'] } });
  assert.equal(created.claimType, 'role');
  assert.equal(created.read.unrestricted, true);
  assert.deepEqual(created.write.ruleNames, ['reach-payments']);
  assert.ok(created.id, 'the store assigned an id');

  const updated = await c.updateSecurityBinding(created.id, { claimType: 'role', claimValue: 'sre', read: { ruleNames: ['reach-core-tenants'] } });
  assert.deepEqual(updated.read.ruleNames, ['reach-core-tenants']);

  await c.deleteSecurityBinding(created.id);
  assert.ok(!(await c.searchSecurityBindings()).bindings.some((b) => b.id === created.id));
});

test('security-binding client methods validate their arguments before calling the server', async () => {
  const c = makeClient();
  await assert.rejects(async () => c.createSecurityBinding({}), TypeError);
  await assert.rejects(async () => c.updateSecurityBinding('x', {}), TypeError);
  await assert.rejects(async () => c.deleteSecurityBinding(''), TypeError);
  await assert.rejects(async () => c.getSecurityBinding(''), TypeError);
});

// ---- access overview (§6.1) ---------------------------------------------------------------------

test('getAccessGrants for Ada projects her binding, administered workflow, and usable credential', async () => {
  const c = makeClient();
  const grantee = { kind: 'person', value: 'u-1042', label: 'Ada Lovelace', identity: [{ dimension: 'sys:sub', value: 'u-1042' }], source: 'directory', complete: true };
  const { bindings, administers, credentialUsage } = await c.getAccessGrants(grantee);
  assert.ok(bindings.some((b) => b.claimType === 'sub' && b.claimValue === 'u-1042'), 'her sub binding is projected');
  assert.ok(administers.some((a) => a.baseWorkflowId === 'nightly-reconcile'), 'the workflow she administers is projected');
  // Only the credential scoped to her identity — shared bindings (usable by any run) are omitted (design §6.1).
  assert.deepEqual(credentialUsage, [{ sourceName: 'billing', environment: 'staging' }]);
});

test('getAccessGrants for the payments team projects the team binding', async () => {
  const c = makeClient();
  const grantee = { kind: 'team', value: 'payments', label: 'Payments', identity: [{ dimension: 'team', value: 'payments' }], source: 'directory', complete: true };
  const { bindings } = await c.getAccessGrants(grantee);
  assert.ok(bindings.some((b) => b.claimType === 'team' && b.claimValue === 'payments'), 'the payments team binding (bind-1) is projected');
});

// ---- identity / grantee resolution (§16.5.4) ----------------------------------------------------

test('searchGrantees returns the resolved grantees with their exact identity and complete flag', async () => {
  const { grantees, nextPageToken } = await makeClient().searchGrantees({ limit: 100 });
  assert.equal(nextPageToken, null);
  assert.ok(grantees.length >= 6);
  const ada = grantees.find((g) => g.value === 'u-1042');
  assert.equal(ada.kind, 'person');
  assert.equal(ada.label, 'Ada Lovelace');
  assert.equal(ada.source, 'directory');
  assert.equal(ada.complete, true);
  assert.deepEqual(ada.identity, [{ dimension: 'sys:iss', value: 'https://idp.example.com' }, { dimension: 'sys:sub', value: 'u-1042' }]);
  const grace = grantees.find((g) => g.value === 'u-2099');
  assert.equal(grace.complete, false, 'an observed-only identity is reported as not complete');
});

test('searchGrantees filters by q, kind and source', async () => {
  const c = makeClient();
  assert.deepEqual((await c.searchGrantees({ q: 'ada' })).grantees.map((g) => g.value), ['u-1042']);
  assert.equal((await c.searchGrantees({ kind: 'person' })).grantees.length, 2);
  assert.ok((await c.searchGrantees({ kind: 'team' })).grantees.every((g) => g.kind === 'team'));
  assert.ok((await c.searchGrantees({ source: 'directory' })).grantees.every((g) => g.source === 'directory'));
  assert.equal((await c.searchGrantees({ q: 'nothing-matches-this' })).grantees.length, 0);
});

test('searchGrantees pages via the keyset nextPageToken', async () => {
  const c = makeClient();
  const first = await c.searchGrantees({ limit: 4 });
  assert.equal(first.grantees.length, 4);
  assert.equal(first.nextPageToken, '4');
  const second = await c.searchGrantees({ limit: 4, pageToken: first.nextPageToken });
  assert.ok(second.grantees.length >= 2);
  assert.equal(second.nextPageToken, null);
  const firstValues = new Set(first.grantees.map((g) => g.value));
  assert.ok(second.grantees.every((g) => !firstValues.has(g.value)), 'pages do not overlap');
});

// ---- environments (§7.7) ------------------------------------------------------------------------

test('listEnvironments returns the seeded environments, ordered by name', async () => {
  const c = makeClient();
  const { environments, nextPageToken } = await c.listEnvironments();
  assert.deepEqual(environments.map((e) => e.name), ['development', 'production', 'staging', 'uat']);
  assert.equal(nextPageToken, null);
});

test('environments are reach-filtered — a reach-scoped caller sees only what its reach admits (non-disclosing)', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  // Administrator (reach: all) sees every environment.
  mock.setPersona('administrator');
  assert.deepEqual((await c.listEnvironments()).environments.map((e) => e.name), ['development', 'production', 'staging', 'uat']);
  // The payments-reach persona: neither seeded environment is tagged domain=payments, so the list is empty and a
  // direct read is reported as absent (404), not forbidden (403) — non-disclosing, mirroring IEnvironmentStore (§7.7).
  mock.setPersona('team-reader');
  assert.deepEqual((await c.listEnvironments()).environments, []);
  await assert.rejects(() => c.getEnvironment('production'), (err) => err instanceof ProblemError && err.status === 404);
});

test('getEnvironment returns one environment and 404s for an unknown name', async () => {
  const c = makeClient();
  const e = await c.getEnvironment('production');
  assert.equal(e.displayName, 'Production');
  await assert.rejects(() => c.getEnvironment('nope'), (err) => err instanceof ProblemError && err.status === 404);
});

test('createEnvironment adds an environment, makes the creator an administrator, and 409s on a duplicate', async () => {
  const c = makeClient();
  const created = await c.createEnvironment({ name: 'qa', displayName: 'QA', description: 'Quality assurance' });
  assert.equal(created.name, 'qa');
  assert.equal(created.displayName, 'QA');
  assert.ok(created.createdBy, 'stamps the creator');
  // creation grants the creator administration of the new environment (§7.7).
  const { administrators } = await c.listEnvironmentAdministrators('qa');
  assert.equal(administrators.length, 1, 'the creator is the sole administrator');
  await assert.rejects(() => c.createEnvironment({ name: 'qa' }), (err) => err.status === 409);
});

test('updateEnvironment replaces the mutable metadata and 404s for an unknown name', async () => {
  const c = makeClient();
  const updated = await c.updateEnvironment('staging', { displayName: 'Staging (EU)', description: 'EU staging' });
  assert.equal(updated.displayName, 'Staging (EU)');
  assert.equal(updated.description, 'EU staging');
  assert.ok(updated.lastUpdatedBy, 'stamps the updater');
  await assert.rejects(() => c.updateEnvironment('nope', { displayName: 'x' }), (err) => err.status === 404);
});

test('deleteEnvironment removes an environment (and its dependent state) and 404s when absent', async () => {
  const c = makeClient();
  await c.createEnvironment({ name: 'qa' });
  await c.deleteEnvironment('qa');
  assert.equal((await c.listEnvironments()).environments.some((e) => e.name === 'qa'), false);
  await assert.rejects(() => c.getEnvironment('qa'), (err) => err.status === 404);
  await assert.rejects(() => c.deleteEnvironment('qa'), (err) => err.status === 404);
});

test('environment administrators: add a resolved grantee, then refuse removing the last one (409)', async () => {
  const c = makeClient();
  const before = (await c.listEnvironmentAdministrators('staging')).administrators.length; // seeded: alice@ops + omar@ops
  const added = await c.addEnvironmentAdministrator('staging', {
    kind: 'team', value: 'payments', identity: [{ dimension: 'team', value: 'payments' }], label: 'Payments',
  });
  assert.equal(added.administrators.length, before + 1, 'the seeded admins plus the added team');
  const payments = added.administrators.find((a) => a.label === 'Payments');
  const removed = await c.removeEnvironmentAdministrator('staging', payments.digest);
  assert.equal(removed.administrators.length, before, 'back to the seeded administrators');
  // Reduce to a single administrator, then the last removal is refused.
  const single = await c.transferEnvironmentAdministration('staging', { administrators: [{ dimension: 'sys:sub', value: 'alice@ops' }] });
  assert.equal(single.administrators.length, 1);
  await assert.rejects(
    () => c.removeEnvironmentAdministrator('staging', single.administrators[0].digest),
    (err) => err.status === 409, 'the set may never be left empty');
});

test('transferEnvironmentAdministration replaces the whole administrator set', async () => {
  const c = makeClient();
  const result = await c.transferEnvironmentAdministration('production', {
    administrators: [{ dimension: 'tenant', value: 'platform' }],
  });
  assert.equal(result.administrators.length, 1);
  assert.ok(result.administrators[0].identity.some((g) => g.dimension === 'tenant' && g.value === 'platform'));
});

test('listEnvironmentAvailability lists the versions made available in an environment, ordered', async () => {
  const c = makeClient();
  const { availability } = await c.listEnvironmentAvailability('production');
  assert.ok(availability.length >= 1, 'production has seeded availability');
  assert.ok(availability.every((a) => a.environment === 'production'), 'only this environment');
  const keys = availability.map((a) => `${a.baseWorkflowId}@${a.versionNumber}`);
  assert.deepEqual(keys, [...keys].sort(), 'ordered by base workflow id then version');
});

test('makeVersionAvailable promotes a ready version (idempotent); deleteVersionAvailability removes it', async () => {
  const c = makeClient();
  // adopt-pet v1 is seeded available in production; withdraw then re-make it to exercise both verbs.
  await c.deleteVersionAvailability('adopt-pet', 1, 'production');
  assert.equal((await c.listVersionAvailability('adopt-pet', 1)).availability.length, 0, 'withdrawn');
  const made = await c.makeVersionAvailable('adopt-pet', 1, 'production');
  assert.equal(made.environment, 'production');
  assert.equal(made.baseWorkflowId, 'adopt-pet');
  // idempotent: making it again succeeds and returns the existing entry.
  const again = await c.makeVersionAvailable('adopt-pet', 1, 'production');
  assert.equal(again.environment, 'production');
  assert.deepEqual((await c.listVersionAvailability('adopt-pet', 1)).availability.map((a) => a.environment), ['production']);
});

test('makeVersionAvailable is readiness-gated (409) and 404s for an unknown environment', async () => {
  const c = makeClient();
  await c.createEnvironment({ name: 'qa' });
  // onboard-customer v1 references a source with no usable credential in the fresh 'qa' environment.
  await assert.rejects(() => c.makeVersionAvailable('onboard-customer', 1, 'qa'), (e) => e.status === 409);
  await assert.rejects(() => c.makeVersionAvailable('adopt-pet', 1, 'no-such-env'), (e) => e.status === 404);
});

test('makeVersionAvailable is evidence-gated where the environment requires it (§4.6)', async () => {
  const c = makeClient();
  await c.createEnvironment({ name: 'prod-eu', requireEvidence: true });
  // Credential the sources first so the §7.7 credential gate passes and the evidence gate is what decides.
  await c.createCredential({ sourceName: 'petstore', environment: 'prod-eu', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'keyvault://pets-eu' }] });
  await c.createCredential({ sourceName: 'billing', environment: 'prod-eu', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'keyvault://billing-eu' }] });

  // adopt-pet v1 carries a RED seeded suite (1/2 passed) → refused; nightly-reconcile v3 is GREEN → admitted.
  await assert.rejects(
    () => c.makeVersionAvailable('adopt-pet', 1, 'prod-eu'),
    (e) => e.status === 409 && /evidence/i.test(e.problem?.title ?? e.message));
  // nightly-reconcile v1 predates evidence entirely → refused the same way.
  await assert.rejects(
    () => c.makeVersionAvailable('nightly-reconcile', 1, 'prod-eu'),
    (e) => e.status === 409 && /evidence/i.test(e.problem?.title ?? e.message));
  const made = await c.makeVersionAvailable('nightly-reconcile', 3, 'prod-eu');
  assert.equal(made.environment, 'prod-eu', 'a green attested suite promotes');
});

test('deleteVersionAvailability 404s when the version is not available in that environment', async () => {
  const c = makeClient();
  await assert.rejects(() => c.deleteVersionAvailability('adopt-pet', 1, 'staging'), (e) => e.status === 404);
});

// ---- persona enforcement (the gated-elevation model) --------------------------------------------

test('persona enforcement: an operator must request promotion and an administrator approves it', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

  // omar@ops administers staging but NOT production. His promotion inbox is per-environment — staging requests only,
  // never production — and a direct make-available in production is refused (not an administrator of it).
  mock.setPersona('operator');
  const opInbox = (await c.listAvailabilityRequests({ scope: 'queue' })).availabilityRequests;
  assert.ok(opInbox.length > 0 && opInbox.every((r) => r.environment === 'staging'), 'operator sees only staging promotion requests');
  await assert.rejects(() => c.makeVersionAvailable('nightly-reconcile', 1, 'production'), (e) => e.status === 403);

  // The operator CAN request promotion (auth-only) but cannot approve it (not an administrator → 403).
  const req = await c.submitAvailabilityRequest({ baseWorkflowId: 'nightly-reconcile', versionNumber: 1, environment: 'production' });
  assert.equal(req.status, 'Pending');
  await assert.rejects(() => c.approveAvailabilityRequest(req.id), (e) => e.status === 403);

  // The administrator sees it in the inbox and approves → the version becomes available.
  mock.setPersona('administrator');
  assert.ok((await c.listAvailabilityRequests({ scope: 'queue' })).availabilityRequests.some((r) => r.id === req.id));
  assert.equal((await c.approveAvailabilityRequest(req.id)).status, 'Approved');
  assert.ok((await c.listVersionAvailability('nightly-reconcile', 1)).availability.some((a) => a.environment === 'production'));
});

test('persona enforcement: a viewer cannot perform run or governance writes but can read', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  mock.setPersona('viewer');

  await assert.rejects(() => c.createEnvironment({ name: 'qa' }), (e) => e.status === 403);             // environments:write
  await assert.rejects(() => c.makeVersionAvailable('adopt-pet', 1, 'production'), (e) => e.status === 403); // availability:write
  await assert.rejects(() => c.cancelRun('run-1'), (e) => e.status === 403);                            // runs:write
  assert.ok((await c.listEnvironments()).environments.length >= 1, 'reads still work');
});

test('per-resource administration: the access-request inbox is scoped to the workflows the persona administers', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

  // omar@ops administers onboard-customer but not nightly-reconcile: his inbox shows only onboard-customer requests, and
  // approving one stamps him as the decider — but he is refused a nightly-reconcile request he does not administer.
  mock.setPersona('operator');
  const inbox = (await c.listAccessRequests({ scope: 'queue' })).accessRequests;
  assert.ok(inbox.length > 0 && inbox.every((r) => r.baseWorkflowId === 'onboard-customer'), 'only onboard-customer requests');
  const mine = inbox.find((r) => r.status === 'Pending');
  const approved = await c.approveAccessRequest(mine.id);
  assert.equal(approved.status, 'Approved');
  assert.equal(approved.decidedBy, 'omar@ops', 'the decision is attributed to the acting identity');

  // A nightly-reconcile request (visible to the admin) is refused to omar — he does not administer that workflow.
  mock.setPersona('administrator');
  const nr = (await c.listAccessRequests({ scope: 'queue' })).accessRequests.find((r) => r.baseWorkflowId === 'nightly-reconcile' && r.status === 'Pending');
  mock.setPersona('operator');
  await assert.rejects(() => c.approveAccessRequest(nr.id), (e) => e.status === 403);
});

test('audit attribution: a submitted request is stamped with the acting identity and appears in "my requests"', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  mock.setPersona('team-reader'); // pat@payments
  const before = (await c.listAccessRequests({ scope: 'mine' })).accessRequests.length;
  const req = await c.submitAccessRequest({ baseWorkflowId: 'nightly-reconcile', requestedScopes: ['runs:write'] });
  assert.equal(req.createdBy, 'pat@payments', 'createdBy is the active identity');
  assert.equal(req.subjectClaimValue, 'pat@payments', 'and the subject is the active identity');
  const after = (await c.listAccessRequests({ scope: 'mine' })).accessRequests;
  assert.equal(after.length, before + 1);
  assert.ok(after.some((r) => r.id === req.id), 'the request appears in the requester’s own view');
});

// ---- runner registry (§5.4) ---------------------------------------------------------------------

test('listRunners returns the registered runners with their transports and hosted versions', async () => {
  const c = makeClient();
  const { runners, nextPageToken } = await c.listRunners();
  assert.equal(runners.length, 3);
  assert.equal(nextPageToken, null);
  const eu1 = runners.find((r) => r.runnerId === 'runner-eu-1');
  assert.ok(eu1.transports.includes('http'));
  assert.ok(eu1.hostedVersions.some((h) => h.baseWorkflowId === 'adopt-pet' && h.loaded === true));
  // The seed includes a version still loading (loaded: false) so the health view can show "loading".
  const eu2 = runners.find((r) => r.runnerId === 'runner-eu-2');
  assert.ok(eu2.hostedVersions.some((h) => h.loaded === false));
});

test('listRunners keyset-pages via the nextPageToken, ordered by runnerId', async () => {
  const c = makeClient();
  const first = await c.listRunners({ limit: 2 });
  assert.equal(first.runners.length, 2);
  assert.equal(first.nextPageToken, '2');
  const seen = [];
  for await (const page of c.listRunnersPaged({ limit: 2 })) seen.push(...page.runners.map((r) => r.runnerId));
  assert.equal(seen.length, 3);
  assert.equal(new Set(seen).size, 3);
  assert.deepEqual(seen, [...seen].sort(), 'ordered by runnerId');
});

// ---- runner authorizations (§5.5) ---------------------------------------------------------------

test('listRunnerAuthorizations is the approver inbox: defaults to Pending across administered environments', async () => {
  const c = makeClient();
  const { authorizations, nextPageToken } = await c.listRunnerAuthorizations();
  assert.equal(nextPageToken, null);
  assert.ok(authorizations.every((a) => a.status === 'Pending'), 'defaults to Pending');
  // The seed lines up with the runner fleet: runner-us-1 (production) and runner-eu-2 (staging) are awaiting a decision.
  assert.deepEqual(authorizations.map((a) => `${a.environment}/${a.runnerId}`), ['production/runner-us-1', 'staging/runner-eu-2']);
});

test('listRunnerAuthorizations filters by status (Authorized / Revoked) and by environment', async () => {
  const c = makeClient();
  const authorized = await c.listRunnerAuthorizations({ status: 'Authorized' });
  assert.deepEqual(authorized.authorizations.map((a) => a.runnerId), ['runner-eu-1']);
  const revoked = await c.listRunnerAuthorizations({ status: 'Revoked' });
  assert.deepEqual(revoked.authorizations.map((a) => a.runnerId), ['runner-eu-old']);
  // An environment narrows to that one environment's queue (still Pending-by-default).
  const prodPending = await c.listRunnerAuthorizations({ environment: 'production' });
  assert.deepEqual(prodPending.authorizations.map((a) => a.runnerId), ['runner-us-1']);
});

test('listRunnerAuthorizations keyset-pages via the nextPageToken, ordered by (environment, runnerId)', async () => {
  const c = makeClient();
  const first = await c.listRunnerAuthorizations({ limit: 1 });
  assert.equal(first.authorizations.length, 1);
  assert.ok(first.nextPageToken);
  const seen = [];
  for await (const page of c.listRunnerAuthorizationsPaged({ limit: 1 })) seen.push(...page.authorizations.map((a) => `${a.environment}/${a.runnerId}`));
  assert.deepEqual(seen, ['production/runner-us-1', 'staging/runner-eu-2']);
});

test('listEnvironmentRunnerAuthorizations is the per-environment roster (all statuses), 404 for an unknown environment', async () => {
  const c = makeClient();
  const { authorizations } = await c.listEnvironmentRunnerAuthorizations('production');
  // Every production authorization, whatever its status, ordered by runnerId.
  assert.deepEqual(authorizations.map((a) => a.runnerId), ['runner-eu-1', 'runner-eu-old', 'runner-us-1']);
  await assert.rejects(() => c.listEnvironmentRunnerAuthorizations('nope'), (e) => e.status === 404);
});

test('authorizeRunner authorizes a pending runner (idempotent) and 404s for an unregistered runner', async () => {
  const c = makeClient();
  const decided = await c.authorizeRunner('production', 'runner-us-1', { reason: 'Vetted.' });
  assert.equal(decided.status, 'Authorized');
  assert.equal(decided.reason, 'Vetted.');
  // It leaves the Pending inbox.
  assert.ok(!(await c.listRunnerAuthorizations()).authorizations.some((a) => a.runnerId === 'runner-us-1'));
  // Idempotent — authorizing again returns it unchanged.
  assert.equal((await c.authorizeRunner('production', 'runner-us-1')).status, 'Authorized');
  // A runner that never registered for the environment is 404.
  await assert.rejects(() => c.authorizeRunner('production', 'ghost-runner'), (e) => e.status === 404);
});

test('revokeRunner revokes an authorization (idempotent) and 404s for an unregistered runner', async () => {
  const c = makeClient();
  const decided = await c.revokeRunner('production', 'runner-eu-1', { reason: 'Rotated out.' });
  assert.equal(decided.status, 'Revoked');
  assert.equal((await c.revokeRunner('production', 'runner-eu-1')).status, 'Revoked'); // idempotent
  await assert.rejects(() => c.revokeRunner('staging', 'ghost-runner'), (e) => e.status === 404);
});

test('runner-authorization client methods validate their arguments before calling the server', async () => {
  const c = makeClient();
  assert.throws(() => c.authorizeRunner('', 'runner-1'), TypeError);
  assert.throws(() => c.authorizeRunner('production', ''), TypeError);
  assert.throws(() => c.revokeRunner('production', ''), TypeError);
  await assert.rejects(() => c.listEnvironmentRunnerAuthorizations(''), TypeError);
});

test('persona enforcement: a non-administrator has an empty runner-authorization inbox and cannot authorize', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  // vera@audit administers no environment → her runner-authorization inbox is empty and she cannot authorize.
  mock.setPersona('viewer');
  assert.equal((await c.listRunnerAuthorizations()).authorizations.length, 0);
  await assert.rejects(() => c.authorizeRunner('production', 'runner-us-1'), (e) => e.status === 403);

  mock.setPersona('administrator');
  assert.ok((await c.listRunnerAuthorizations()).authorizations.some((a) => a.runnerId === 'runner-us-1'));
  assert.equal((await c.authorizeRunner('production', 'runner-us-1')).status, 'Authorized');
});

// ---- designer workspace (workflow-designer design §4.1) --------------------------------------------

test('createWorkingCopy round-trips the document and designer state, and list omits them', async () => {
  const c = makeClient();
  const created = await c.createWorkingCopy({
    name: 'retry tuning',
    document: { arazzo: '1.1.0', info: { title: 'Nightly' }, workflows: [{ workflowId: 'nightly-reconcile', steps: [] }] },
    designerState: { nodes: { 'step-1': { x: 40, y: 80 } } },
  });
  assert.ok(created.id);
  assert.ok(created.etag);
  assert.equal(created.name, 'retry tuning');
  assert.equal(created.document.info.title, 'Nightly');
  assert.equal(created.designerState.nodes['step-1'].x, 40);

  const single = await c.getWorkingCopy(created.id);
  assert.equal(single.document.info.title, 'Nightly');

  const { workingCopies } = await c.listWorkingCopies();
  const entry = workingCopies.find((w) => w.id === created.id);
  assert.equal(entry.name, 'retry tuning');
  assert.equal(entry.document, undefined);
  assert.equal(entry.designerState, undefined);
});

test('createWorkingCopy derives the name from the first workflowId, or untitled with a skeleton', async () => {
  const c = makeClient();
  const derived = await c.createWorkingCopy({ document: { arazzo: '1.1.0', workflows: [{ workflowId: 'pay-invoice', steps: [] }] } });
  assert.equal(derived.name, 'pay-invoice');

  const blank = await c.createWorkingCopy();
  assert.equal(blank.name, 'untitled');
  assert.equal(blank.document.arazzo, '1.1.0');
  assert.equal(blank.document.info.title, 'untitled');
});

test('saveWorkingCopy is etag-guarded: a stale save 409s and clobbers nothing', async () => {
  const c = makeClient();
  const created = await c.createWorkingCopy({ name: 'wc', document: { arazzo: '1.1.0', 'x-rev': 1 } });

  const saved = await c.saveWorkingCopy(created.id, { document: { arazzo: '1.1.0', 'x-rev': 2 }, expectedEtag: created.etag });
  assert.equal(saved.document['x-rev'], 2);
  assert.notEqual(saved.etag, created.etag);
  assert.ok(saved.lastUpdatedBy);

  // A collaborator saving with the etag they read earlier conflicts.
  await assert.rejects(
    () => c.saveWorkingCopy(created.id, { document: { arazzo: '1.1.0', 'x-rev': 3 }, expectedEtag: created.etag }),
    (e) => e instanceof ProblemError && e.status === 409);
  assert.equal((await c.getWorkingCopy(created.id)).document['x-rev'], 2);

  // The etag is not optional.
  assert.throws(() => c.saveWorkingCopy(created.id, { document: { arazzo: '1.1.0' } }), TypeError);
});

test('deleteWorkingCopy removes it and a later get 404s', async () => {
  const c = makeClient();
  const created = await c.createWorkingCopy({ name: 'wc', document: { arazzo: '1.1.0' } });
  await c.deleteWorkingCopy(created.id);
  await assert.rejects(() => c.getWorkingCopy(created.id), (e) => e instanceof ProblemError && e.status === 404);
});

test('listWorkingCopiesPaged walks every page via the token', async () => {
  const c = makeClient();
  for (let i = 0; i < 5; i++) await c.createWorkingCopy({ name: `wc ${i}`, document: { arazzo: '1.1.0' } });
  let total = 0; let pages = 0;
  for await (const page of c.listWorkingCopiesPaged({ limit: 2 })) { total += page.workingCopies.length; pages++; }
  assert.equal(total, 5);
  assert.equal(pages, 3);
});

test('validateWorkingCopy reports positioned findings and 404s for an unknown id', async () => {
  const c = makeClient();

  // The blank skeleton is deliberately not yet valid Arazzo — findings, not an error response.
  const blank = await c.createWorkingCopy();
  const outcome = await c.validateWorkingCopy(blank.id);
  assert.equal(outcome.valid, false);
  assert.ok(outcome.diagnostics.every((d) => d.category === 'schema' && d.severity === 'error'));

  // Semantically broken: a goto to a step that does not exist, positioned by JSON Pointer.
  const broken = await c.createWorkingCopy({
    document: {
      arazzo: '1.1.0',
      info: { title: 'T', version: '1.0.0' },
      sourceDescriptions: [{ name: 'pets', url: './pets.json', type: 'openapi' }],
      workflows: [{ workflowId: 'w', steps: [{ stepId: 'a', operationId: 'x', onSuccess: [{ name: 'j', type: 'goto', stepId: 'ghost' }] }] }],
    },
  });
  const semantic = await c.validateWorkingCopy(broken.id);
  assert.equal(semantic.valid, false);
  const finding = semantic.diagnostics.find((d) => d.category === 'goto-target');
  assert.equal(finding.instancePath, '/workflows/0/steps/0/onSuccess/0');

  await assert.rejects(() => c.validateWorkingCopy('nope'), (e) => e instanceof ProblemError && e.status === 404);
});

test('sources attach inline or by registry reference, list without documents, and project operations', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({ name: 'wc', document: { arazzo: '1.1.0' } });
  const petstore = {
    openapi: '3.1.0',
    info: { title: 'Petstore', version: '1.0' },
    paths: { '/pets': { get: { operationId: 'listPets', responses: { 200: { description: 'ok', content: { 'application/json': { schema: { type: 'array', items: { type: 'object', properties: { name: { type: 'string' } } } } } } }, default: { description: 'unexpected' } } } } },
  };

  // Inline attach: type detected, document never echoed, the fresh etag flows back.
  const attached = await c.attachWorkingCopySource(wc.id, 'pets', { document: petstore });
  assert.equal(attached.kind, 'inline');
  assert.equal(attached.type, 'openapi');
  assert.equal(attached.document, undefined);
  assert.notEqual(attached.etag, wc.etag);

  const { sources } = await c.listWorkingCopySources(wc.id);
  assert.equal(sources.length, 1);
  assert.equal(sources[0].document, undefined);

  const { operations } = await c.listWorkingCopySourceOperations(wc.id, 'pets');
  assert.equal(operations[0].operationId, 'listPets');
  assert.equal(operations[0].method, 'GET');
  assert.deepEqual(Object.keys(operations[0].responses), ['200', 'default']);
  assert.equal(operations[0].responses['200'].schema.items.properties.name.type, 'string');

  // Registry attach: resolves at read time; the registry-side surface projects too.
  await c.createSource({ name: 'petstore-testonly', type: 'openapi', document: petstore });
  const viaRegistry = await c.attachWorkingCopySource(wc.id, 'pets2', { sourceName: 'petstore-testonly' });
  assert.equal(viaRegistry.kind, 'registry');
  assert.equal((await c.listWorkingCopySourceOperations(wc.id, 'pets2')).operations[0].operationId, 'listPets');
  assert.equal((await c.listRegisteredSourceOperations('petstore-testonly')).operations[0].operationId, 'listPets');

  // Detach removes; unknown names 404; ambiguity throws client-side.
  await c.detachWorkingCopySource(wc.id, 'pets');
  assert.equal((await c.listWorkingCopySources(wc.id)).sources.length, 1);
  await assert.rejects(() => c.listWorkingCopySourceOperations(wc.id, 'pets'), (e) => e.status === 404);
  assert.throws(() => c.attachWorkingCopySource(wc.id, 'x', {}), TypeError);
  assert.throws(() => c.attachWorkingCopySource(wc.id, 'x', { sourceName: 'a', document: {} }), TypeError);
});

test('fetchSourceDocument returns the validated document with detected type and digest', async () => {
  const c = makeClient();
  const fetched = await c.fetchSourceDocument({ url: 'https://specs.example/petstore.json' });
  assert.equal(fetched.url, 'https://specs.example/petstore.json');
  assert.ok(['openapi', 'asyncapi', 'arazzo'].includes(fetched.type));
  assert.ok(fetched.digest);
  assert.ok(fetched.document);

  await assert.rejects(() => c.fetchSourceDocument({ url: 'http://insecure.example/x.json' }), (e) => e.status === 400);
  assert.throws(() => c.fetchSourceDocument({}), TypeError);
});

test('every seeded registry source projects a non-empty operation surface', async () => {
  const c = makeClient();
  const { sources } = await c.listSources({ limit: 50 });
  assert.ok(sources.length >= 3, 'the registry seeds at least three sources');
  for (const s of sources) {
    const { operations } = await c.listRegisteredSourceOperations(s.name);
    assert.ok(operations.length > 0, `seeded source '${s.name}' must list operations — an empty rail after attaching it reads as a bug`);
  }
});

test('simulateWorkingCopy returns a structured trace from the mock synthesizer', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'sim',
    document: {
      arazzo: '1.1.0',
      info: { title: 't', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{
        workflowId: 'wf',
        steps: [
          { stepId: 'a', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }] },
          { stepId: 'b', operationId: 'adoptPet' },
        ],
      }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  const trace = await c.simulateWorkingCopy(wc.id, {
    scenario: { mocks: [
      { method: 'get', path: '/pets', status: 200, body: [] },
      { method: 'post', path: '/pets/{petId}/adopt', status: 200 },
    ] },
  });
  assert.equal(trace.outcome, 'completed');
  assert.equal(trace.steps.length, 2);
  assert.equal(trace.steps[0].successCriteria[0].satisfied, true);

  // STATELESS stepping: every command re-supplies the whole scenario and replays from the start.
  const paused = await c.simulateWorkingCopy(wc.id, {
    scenario: { mocks: [{ method: 'get', path: '/pets', status: 200, body: [] }] },
    until: { beforeStepId: 'b' },
  });
  assert.equal(paused.outcome, 'paused');
  assert.equal(paused.pausedBefore, 'b');
  assert.equal(paused.steps.length, 1);
});

test('validate flags source-integrity drift: undeclared attachment, missing attachment, unresolvable operation', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'drift',
    document: {
      arazzo: '1.1.0',
      info: { title: 't', version: '1' },
      sourceDescriptions: [{ name: 'ghost', url: './ghost.json', type: 'openapi' }],
      workflows: [{ workflowId: 'wf', steps: [{ stepId: 'a', operationId: 'noSuchOp' }] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  const report = await c.validateWorkingCopy(wc.id);
  const cats = report.diagnostics.filter((d) => d.category === 'workspace-sources');
  assert.ok(cats.some((d) => d.severity === 'warning' && d.message.includes("'ghost' has no attachment")), 'declared-but-unattached flagged');
  assert.ok(cats.some((d) => d.severity === 'info' && d.message.includes("'petstore' is not declared")), 'attached-but-undeclared flagged');
  assert.ok(cats.some((d) => d.severity === 'warning' && d.message.includes("'noSuchOp' is not found")), 'unresolvable operation flagged');

  // No sourceDescriptions at all while steps bind operations → an ERROR.
  const bare = await c.createWorkingCopy({
    name: 'bare',
    document: { arazzo: '1.1.0', info: { title: 't', version: '1' }, workflows: [{ workflowId: 'wf', steps: [{ stepId: 'a', operationId: 'x' }] }] },
  });
  const bareReport = await c.validateWorkingCopy(bare.id);
  assert.equal(bareReport.valid, false);
  assert.ok(bareReport.diagnostics.some((d) => d.category === 'workspace-sources' && d.severity === 'error'));
});

test('scenarios: lifecycle + judged runs through the mock', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'sc', document: {
      arazzo: '1.1.0', info: { title: 't', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{ workflowId: 'wf', steps: [
        { stepId: 'a', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }] },
      ] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  await c.putScenario(wc.id, {
    name: 'ok', mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 200 }] }],
    expect: { outcome: 'completed', steps: { a: { attempts: 1 } } },
  });
  await c.putScenario(wc.id, {
    name: 'sad', mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 500 }] }],
    expect: { outcome: 'completed' },
  });
  assert.equal((await c.listScenarios(wc.id)).scenarios.length, 2);

  const one = await c.runScenario(wc.id, 'ok');
  assert.equal(one.passed, true);
  assert.equal(one.trace.steps.length, 1);

  const suite = await c.runAllScenarios(wc.id);
  assert.equal(suite.total, 2);
  assert.equal(suite.failed, 1);

  await c.deleteScenario(wc.id, 'sad');
  assert.equal((await c.listScenarios(wc.id)).scenarios.length, 1);
});

test('publish attests the suite and serves evidence; failures refuse with 422', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'pub', document: {
      arazzo: '1.1.0', info: { title: 'Pub', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{ workflowId: 'pub-flow', steps: [{ stepId: 'a', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }] }] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });
  await c.putScenario(wc.id, { name: 'ok', mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 200 }] }], expect: { outcome: 'completed' } });

  const version = await c.publishWorkingCopy(wc.id, { owner: { name: 'Team', email: 't@example.com' }, tags: ['designer'] });
  assert.equal(version.baseWorkflowId, 'pub-flow');

  const evidence = await c.getCatalogEvidence(version.baseWorkflowId, version.versionNumber);
  assert.equal(evidence.suite.passed, 1);
  assert.equal(evidence.scenarios[0].name, 'ok');
  assert.ok(evidence.scenarios[0].pathSummary.includes('a'));

  // A failing scenario refuses.
  await c.putScenario(wc.id, { name: 'sad', mocks: [{ source: 'petstore', operationId: 'listPets', responses: [{ status: 500 }] }], expect: { outcome: 'completed' } });
  await assert.rejects(() => c.publishWorkingCopy(wc.id, { owner: { name: 'T', email: 't@e.com' } }), (e) => e.status === 422);
});

// ---- brokered GitHub (workflow-designer design §4.7) ----------------------------------------------

test('the brokered GitHub flow: begin → callback (single-use state) → session → browse → disconnect', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

  assert.equal((await c.getGitHubStatus()).connected, false, 'starts disconnected');

  // Begin returns the authorize URL the kit opens in a POPUP; the mock's points at its own callback,
  // so the "popup navigation" below completes the sign-in immediately.
  const { authorizeUrl, state } = await c.beginGitHubAuth();
  assert.ok(authorizeUrl.includes('/github/auth/callback'), 'the mock self-completes via its own callback');
  assert.ok(authorizeUrl.includes(encodeURIComponent(state)), 'the state rides the authorize URL');
  assert.equal((await mock.fetch(authorizeUrl)).status, 200, 'the callback signs in (state-authenticated, no bearer)');
  assert.equal((await mock.fetch(authorizeUrl)).status, 400, 'the state is single-use: a replay refuses');

  const session = await c.getGitHubStatus();
  assert.equal(session.connected, true);
  assert.equal(session.login, 'octo');
  assert.equal(session.installations[0].account, 'acme-org');
  assert.equal(session.installations[0].repositories[0].fullName, 'acme-org/specs');

  const root = await c.browseRepo('acme-org', 'specs');
  assert.equal(root.kind, 'dir');
  assert.ok(root.entries.some((e) => e.name === 'petstore.openapi.json' && e.type === 'file'), 'the root lists the spec');
  const file = await c.browseRepo('acme-org', 'specs', { path: 'petstore.openapi.json' });
  assert.equal(file.kind, 'file');
  assert.ok(JSON.parse(Buffer.from(file.file.content, 'base64').toString()).openapi, 'the file content decodes to the spec');
  await assert.rejects(() => c.browseRepo('acme-org', 'elsewhere'), (e) => e.status === 404, 'an unreachable repo is not found');

  await c.deleteGitHubSession();
  assert.equal((await c.getGitHubStatus()).connected, false, 'disconnect drops the session');
  await assert.rejects(() => c.browseRepo('acme-org', 'specs'), (e) => e.status === 409, 'browse without a session conflicts');
});

test('persona enforcement: a viewer cannot begin the GitHub sign-in (workspace:write)', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  mock.setPersona('viewer');
  await assert.rejects(() => c.beginGitHubAuth(), (e) => e.status === 403);
});

test('a Git-bound working copy commits to and pulls from its branch (§4.7)', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

  // Connect, then bind a working copy (document + one scenario) to a branch.
  const { authorizeUrl } = await c.beginGitHubAuth();
  await mock.fetch(authorizeUrl);
  const wc = await c.createWorkingCopy({ name: 'adopt', document: { arazzo: '1.1.0', info: { title: 'Adopt', version: '1' }, workflows: [{ workflowId: 'adopt', steps: [] }] } });
  await c.putScenario(wc.id, { name: 'happy', expect: { outcome: 'completed' } });
  // The branch must exist before anything writes to it (a ref, no commit; §4.7).
  const branches = await c.listRepoBranches('acme-org', 'specs');
  assert.equal(branches.defaultBranch, 'main');
  const createdBranch = await c.createRepoBranch('acme-org', 'specs', { name: 'feature/adopt' });
  assert.equal(createdBranch.name, 'feature/adopt');
  await assert.rejects(() => c.createRepoBranch('acme-org', 'specs', { name: 'feature/adopt' }), (e) => e.status === 409);

  const fresh = await c.getWorkingCopy(wc.id);
  const bound = await c.saveWorkingCopy(wc.id, {
    document: fresh.document,
    expectedEtag: fresh.etag,
    gitBinding: { owner: 'acme-org', repo: 'specs', branch: 'feature/adopt', path: 'flows/adopt.arazzo.json', scenariosDir: 'scenarios/adopt' },
  });
  assert.equal(bound.gitBinding.branch, 'feature/adopt', 'the binding persists on save');

  // Commit: the document and the scenario file land in the repo; the draft PR opens from the branch.
  const committed = await c.commitWorkingCopy(wc.id, { message: 'sync', pullRequest: { base: 'main', draft: true } });
  assert.deepEqual(committed.files.map((f) => f.path), ['flows/adopt.arazzo.json', 'scenarios/adopt/happy.scenario.json']);
  assert.ok(committed.pullRequest.url.includes('/pull/'), 'the pull request opened');
  const committedDoc = await c.browseRepo('acme-org', 'specs', { path: 'flows/adopt.arazzo.json' });
  assert.equal(JSON.parse(Buffer.from(committedDoc.file.content, 'base64').toString()).info.title, 'Adopt');

  // A repo-side edit pulls back in under the etag guard.
  const scenario = await c.browseRepo('acme-org', 'specs', { path: 'scenarios/adopt/happy.scenario.json' });
  assert.ok(scenario.kind === 'file', 'the scenario file landed');
  const pulled = await c.pullWorkingCopy(wc.id, { expectedEtag: bound.etag });
  assert.equal(pulled.document.info.title, 'Adopt', 'pull round-trips the committed document');
  await assert.rejects(() => c.pullWorkingCopy(wc.id, { expectedEtag: bound.etag }), (e) => e.status === 409, 'a stale etag conflicts');

  // Unbound copies refuse; disconnected sessions conflict.
  const unbound = await c.createWorkingCopy({ name: 'plain', document: { arazzo: '1.1.0', info: { title: 'x', version: '1' }, workflows: [] } });
  await assert.rejects(() => c.commitWorkingCopy(unbound.id, { message: 'x' }), (e) => e.status === 400);
  await c.deleteGitHubSession();
  await assert.rejects(() => c.commitWorkingCopy(wc.id, { message: 'x' }), (e) => e.status === 409);
});

test('git history browses, compares at a ref, and rolls back via pull-at-ref (snag 9)', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  const { authorizeUrl } = await c.beginGitHubAuth();
  await mock.fetch(authorizeUrl);

  // History pages newest-first, scoped to the bound branch and document.
  const first = await c.listRepoCommits('acme-org', 'specs', { sha: 'main', path: 'flows/adopt.arazzo.json', perPage: 2 });
  assert.equal(first.commits.length, 2);
  assert.equal(first.hasMore, true, 'a full page implies more history');
  const second = await c.listRepoCommits('acme-org', 'specs', { sha: 'main', perPage: 2, page: 2 });
  assert.equal(second.commits.length, 1);
  assert.equal(second.hasMore, false);
  const oldest = second.commits[0];
  assert.ok(oldest.sha && oldest.message && oldest.author && oldest.date, 'a commit carries sha, message, author, and date');

  // Contents at a commit's ref serve THAT commit's file — the side-by-side comparison's data.
  const head = await c.browseRepo('acme-org', 'specs', { path: 'flows/adopt.arazzo.json' });
  const historic = await c.browseRepo('acme-org', 'specs', { path: 'flows/adopt.arazzo.json', ref: oldest.sha });
  const headSteps = JSON.parse(Buffer.from(head.file.content, 'base64').toString()).workflows[0].steps.length;
  const historicSteps = JSON.parse(Buffer.from(historic.file.content, 'base64').toString()).workflows[0].steps.length;
  assert.ok(historicSteps < headSteps, 'the historic document differs from the head');

  // Rollback = pull at the commit: the working copy becomes that commit's state. The binding is
  // unchanged — the next commit records the rollback as a new commit on the branch.
  const wc = await c.createWorkingCopy({ name: 'adopt', document: { arazzo: '1.1.0', info: { title: 'Adopt', version: '1' }, workflows: [{ workflowId: 'adopt', steps: [] }] } });
  const fresh = await c.getWorkingCopy(wc.id);
  const bound = await c.saveWorkingCopy(wc.id, {
    document: fresh.document,
    expectedEtag: fresh.etag,
    gitBinding: { owner: 'acme-org', repo: 'specs', branch: 'main', path: 'flows/adopt.arazzo.json' },
  });
  const rolledBack = await c.pullWorkingCopy(wc.id, { expectedEtag: bound.etag, ref: oldest.sha });
  assert.equal(rolledBack.document.workflows[0].steps.length, historicSteps, 'the rollback pulled the commit state');
  assert.equal(rolledBack.gitBinding.branch, 'main', 'the binding is unchanged by a rollback');
});

test('simulateCatalogVersion replays a published version verbatim (§4.3)', async () => {
  const c = makeClient();
  // The seeded adopt-pet v1 carries its packaged document; the simulator runs it with scripted mocks —
  // same request and trace shapes as a working-copy simulate, without any working copy.
  const trace = await c.simulateCatalogVersion('adopt-pet', 1, {
    scenario: { mocks: [
      { method: 'get', path: '/pets/{petId}', status: 200, body: { name: 'Fido' } },
      { method: 'post', path: '/pets/{petId}/adopt', status: 200 },
      { method: 'get', path: '/pets/{petId}/status', status: 200, body: { state: 'adopted' } },
      { method: 'post', path: '/pets/{petId}/confirm', status: 200 },
    ] },
  });
  assert.ok(['completed', 'faulted'].includes(trace.outcome), 'the packaged document simulated to a terminal trace');
  assert.ok(trace.steps.length > 0, 'the trace carries the visited steps');

  await assert.rejects(() => c.simulateCatalogVersion('adopt-pet', 1, { workflowId: 'ghost' }), (e) => e.status === 400);
  await assert.rejects(() => c.simulateCatalogVersion('no-such', 1, {}), (e) => e.status === 404);
});

test('a message wait suspends the simulation; a staged trigger releases it (§3.3 injection)', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'eventful',
    document: {
      arazzo: '1.1.0', info: { title: 'e', version: '1' },
      sourceDescriptions: [
        { name: 'petstore', url: './p.json', type: 'openapi' },
        { name: 'events', url: './e.json', type: 'asyncapi' },
      ],
      workflows: [{ workflowId: 'wf', steps: [
        { stepId: 'list', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }] },
        { stepId: 'confirmed', operationId: 'onOrderEvent' },
        { stepId: 'done', operationId: 'listPets' },
      ] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });
  await c.attachWorkingCopySource(wc.id, 'events', { sourceName: 'events' });
  const mocks = [{ method: 'get', path: '/pets', status: 200, body: [] }];

  // No trigger staged: the run SUSPENDS on the channel, naming what it waits for.
  const suspended = await c.simulateWorkingCopy(wc.id, { scenario: { mocks } });
  assert.equal(suspended.outcome, 'suspended');
  assert.equal(suspended.wait.kind, 'message');
  assert.equal(suspended.wait.channel, 'orders/events');
  assert.equal(suspended.steps.length, 1, 'only the step before the wait executed');

  // The injected trigger joins the scenario; the stateless replay delivers it at the wait.
  const released = await c.simulateWorkingCopy(wc.id, {
    scenario: { mocks, triggers: [{ channel: 'orders/events', payload: { orderId: 'o-1', kind: 'confirmed' } }] },
  });
  assert.equal(released.outcome, 'completed');
  assert.equal(released.steps.length, 3);
  assert.equal(released.steps[1].requests[0].responseBody.orderId, 'o-1', 'the payload rides the trace');
});

test('validate flags payload literals that can never satisfy the operation schema (§5.3)', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'typed-payloads',
    document: {
      arazzo: '1.1.0', info: { title: 't', version: '1' },
      sourceDescriptions: [{ name: 'payments', url: './p.json', type: 'openapi' }],
      workflows: [{ workflowId: 'wf', steps: [{
        stepId: 'authorize', operationId: 'authorize',
        requestBody: { payload: { capture: 'tru', reference: '$inputs.orderId' } },
      }] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'payments', { document: {
    openapi: '3.1.0', info: { title: 'Payments', version: '1' },
    paths: { '/authorize': { post: { operationId: 'authorize', requestBody: { content: { 'application/json': { schema: {
      type: 'object', required: ['amount'],
      properties: { capture: { type: 'boolean' }, amount: { type: 'number' }, reference: { type: 'string' } },
    } } } }, responses: { 200: { description: 'ok' } } } } },
  } });

  const report = await c.validateWorkingCopy(wc.id);
  assert.equal(report.valid, false, 'a boolean leaf holding "tru" cannot be valid');
  const typing = report.diagnostics.filter((d) => d.category === 'payload-typing');
  const bad = typing.find((d) => d.severity === 'error');
  assert.match(bad.message, /'tru' is neither a boolean nor a runtime expression/);
  assert.equal(bad.instancePath, '/workflows/0/steps/0/requestBody/payload/capture');
  assert.ok(typing.some((d) => d.severity === 'warning' && d.message.includes("'amount' is missing")), 'missing required warns');

  // A statically-typed expression must match too: $inputs.orderId is a STRING by the workflow's
  // own inputs schema — placing it on the boolean leaf is an error, not a benefit of the doubt.
  const fresh0 = await c.getWorkingCopy(wc.id);
  fresh0.document.workflows[0].inputs = { type: 'object', properties: { orderId: { type: 'string' } } };
  fresh0.document.workflows[0].steps[0].requestBody.payload = { capture: '$inputs.orderId', amount: 12 };
  await c.saveWorkingCopy(wc.id, { document: fresh0.document, expectedEtag: fresh0.etag });
  const mismatch = await c.validateWorkingCopy(wc.id);
  const exprBad = mismatch.diagnostics.find((d) => d.category === 'payload-typing' && d.severity === 'error');
  assert.match(exprBad.message, /resolves to a string — the operation's schema requires a boolean/);

  // Expressions are exempt wherever they appear; fixing the literal clears the error.
  const fresh = await c.getWorkingCopy(wc.id);
  fresh.document.workflows[0].steps[0].requestBody.payload = { capture: '$inputs.capture', amount: 12 };
  await c.saveWorkingCopy(wc.id, { document: fresh.document, expectedEtag: fresh.etag });
  const clean = await c.validateWorkingCopy(wc.id);
  assert.ok(!clean.diagnostics.some((d) => d.category === 'payload-typing'), 'expressions + literals of the right type pass');
});

test('a workflowId-bound step runs its sub-workflow inline and carries the nested trace (§3.3 step-into)', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'nested',
    document: {
      arazzo: '1.1.0', info: { title: 'n', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [
        { workflowId: 'outer', steps: [
          { stepId: 'run-inner', workflowId: 'inner', onSuccess: [{ name: 'done', type: 'end' }] },
        ] },
        { workflowId: 'inner', outputs: { first: '$steps.list.outputs.first' }, steps: [
          { stepId: 'list', operationId: 'listPets', successCriteria: [{ condition: '$statusCode == 200' }], outputs: { first: '$response.body#/0/name' } },
        ] },
      ],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  const trace = await c.simulateWorkingCopy(wc.id, {
    workflowId: 'outer',
    scenario: { mocks: [{ method: 'get', path: '/pets', status: 200, body: [{ name: 'Biscuit' }] }] },
  });
  assert.equal(trace.outcome, 'completed');
  const record = trace.steps[0];
  assert.equal(record.stepId, 'run-inner');
  assert.equal(record.subTrace.workflowId, 'inner', 'the nested trace names its workflow');
  assert.equal(record.subTrace.steps[0].stepId, 'list', 'the sub-workflow steps ride the record');
  assert.equal(record.outputs.first, 'Biscuit', 'the sub-workflow outputs become the step outputs');
});

test('the trace records what a step actually SENT: resolved path, query, and request body (§3.3)', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'sent-view',
    document: {
      arazzo: '1.1.0', info: { title: 's', version: '1' },
      sourceDescriptions: [{ name: 'orders', url: './o.json', type: 'openapi' }],
      workflows: [{
        workflowId: 'wf',
        inputs: { type: 'object', properties: { orderId: { type: 'string' }, amount: { type: 'number' } } },
        steps: [{
          stepId: 'place', operationId: 'placeOrder',
          parameters: [
            { name: 'orderId', in: 'path', value: '$inputs.orderId' },
            { name: 'dryRun', in: 'query', value: true },
          ],
          requestBody: { payload: { amount: '$inputs.amount', note: 'from-test' } },
          successCriteria: [{ condition: '$statusCode == 201' }],
        }],
      }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'orders', { document: {
    openapi: '3.1.0', info: { title: 'Orders', version: '1' },
    paths: { '/orders/{orderId}': { post: { operationId: 'placeOrder',
      parameters: [{ name: 'orderId', in: 'path', required: true, schema: { type: 'string' } }],
      requestBody: { content: { 'application/json': { schema: { type: 'object', properties: { amount: { type: 'number' }, note: { type: 'string' } } } } } },
      responses: { 201: { description: 'placed' } } } } },
  } });

  const trace = await c.simulateWorkingCopy(wc.id, {
    scenario: { inputs: { orderId: 'o-77', amount: 12.5 }, mocks: [{ method: 'post', path: '/orders/{orderId}', status: 201 }] },
  });
  const sent = trace.steps[0].requests[0];
  assert.equal(sent.path, '/orders/o-77?dryRun=true', 'parameters resolve into the path and query');
  assert.deepEqual(sent.requestBody, { amount: 12.5, note: 'from-test' }, 'the body records as sent, expressions resolved');
});

test('step over: overridden outputs replace execution — no exchange, downstream steps see them (§3.3)', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'what-if',
    document: {
      arazzo: '1.1.0', info: { title: 'w', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{ workflowId: 'wf', steps: [
        { stepId: 'find', operationId: 'getPet', parameters: [{ name: 'petId', in: 'path', value: '$inputs.petId' }],
          successCriteria: [{ condition: '$statusCode == 200' }], outputs: { petName: '$response.body#/name' } },
        { stepId: 'adopt', operationId: 'adoptPet', parameters: [{ name: 'petId', in: 'path', value: '$steps.find.outputs.petName' }],
          successCriteria: [{ condition: '$statusCode == 200' }] },
      ] }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  const trace = await c.simulateWorkingCopy(wc.id, {
    scenario: { inputs: { petId: 'p-1' }, mocks: [{ method: 'post', path: '/pets/{petId}/adopt', status: 200 }] },
    overrides: { stepOutputs: { find: { petName: 'Hypothetical' } } },
  });
  assert.equal(trace.outcome, 'completed');
  const skipped = trace.steps[0];
  assert.equal(skipped.skipped, true, 'the overridden step did not execute');
  assert.equal(skipped.requests, undefined, 'no exchange happened');
  assert.deepEqual(skipped.outputs, { petName: 'Hypothetical' }, 'the provided outputs stand');
  assert.ok(trace.steps[1].requests[0].path.includes('Hypothetical'), 'downstream steps resolve against them');
});

test('an attachment reads back whole — the restore payload for undoing a detach', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({ name: 'detachable', document: {
    arazzo: '1.1.0', info: { title: 'd', version: '1' },
    sourceDescriptions: [{ name: 'inline-src', url: './s.json', type: 'openapi' }],
    workflows: [{ workflowId: 'w', steps: [] }],
  } });
  const doc = { openapi: '3.1.0', info: { title: 'S', version: '1' }, paths: { '/x': { get: { operationId: 'x', responses: { 200: { description: 'ok' } } } } } };
  await c.attachWorkingCopySource(wc.id, 'inline-src', { document: doc });

  const attachment = await c.getWorkingCopySource(wc.id, 'inline-src');
  assert.equal(attachment.name, 'inline-src');
  assert.deepEqual(attachment.document.paths['/x'].get.operationId, 'x', 'the stored inline document comes back');

  await c.detachWorkingCopySource(wc.id, 'inline-src');
  await assert.rejects(() => c.getWorkingCopySource(wc.id, 'inline-src'), /404|not found/i);

  // Re-attach the stash verbatim: the round trip restores the operations surface.
  await c.attachWorkingCopySource(wc.id, 'inline-src', { document: attachment.document });
  const ops = await c.listWorkingCopySourceOperations(wc.id, 'inline-src');
  assert.ok(ops.operations.some((o) => o.operationId === 'x'), 'restored source projects its operations again');
});

test('debug runs (§18): gated start, single-step forward, step-over via Skip, cancel', async () => {
  const c = makeClient();
  const wc = await c.createWorkingCopy({
    name: 'debuggable',
    document: {
      arazzo: '1.1.0', info: { title: 'dbg', version: '1' },
      sourceDescriptions: [{ name: 'petstore', url: './p.json', type: 'openapi' }],
      workflows: [{
        workflowId: 'wf',
        inputs: { type: 'object', properties: { petId: { type: 'string' } } },
        steps: [
          { stepId: 'find', operationId: 'getPet', parameters: [{ name: 'petId', in: 'path', value: '$inputs.petId' }],
            successCriteria: [{ condition: '$statusCode == 200' }], outputs: { petName: '$response.body#/name' } },
          { stepId: 'adopt', operationId: 'adoptPet', parameters: [{ name: 'petId', in: 'path', value: '$inputs.petId' }],
            successCriteria: [{ condition: '$statusCode == 200' }] },
        ],
      }],
    },
  });
  await c.attachWorkingCopySource(wc.id, 'petstore', { sourceName: 'petstore' });

  // Gate 1: an environment that does not allow drafts refuses with 403.
  await assert.rejects(() => c.startDebugRun(wc.id, { workflowId: 'wf', environment: 'production' }), (err) => err.status === 403);

  // Gate 2: readiness — 'petstore' has no credential bound in development yet → 409 naming it.
  await assert.rejects(() => c.startDebugRun(wc.id, { workflowId: 'wf', environment: 'development' }), (err) =>
    err.status === 409 && /petstore/.test(err.problem?.detail ?? ''));
  await c.createCredential({ sourceName: 'petstore', environment: 'development', authKind: 'httpBearer', secretRefs: [{ name: 'token', ref: 'vault://kv/dev/petstore#token' }] });

  // §18 R5: advance is ASYNC — start/resume MARK the run and a runner advances it out-of-band, so the client PUMPS
  // get-debug-run until it settles (it never trusts the mark response's un-advanced trace).
  const SETTLED = new Set(['paused', 'suspended', 'completed', 'faulted', 'cancelled']);
  const pump = async (runId) => {
    let r;
    for (let i = 0; i < 50; i++) { r = await c.getDebugRun(wc.id, runId); if (SETTLED.has(r.status)) return r; }
    throw new Error(`debug run ${runId} did not settle (last: ${r?.status})`);
  };

  // Forward-only lifecycle: start enqueues (un-advanced 'running'); pumping observes paused-after-step-1.
  const started = await c.startDebugRun(wc.id, { workflowId: 'wf', environment: 'development', inputs: { petId: 'p-1' }, pause: { afterEachStep: true } });
  assert.equal(started.status, 'running', 'the enqueue response is un-advanced — a runner advances it');
  const run = await pump(started.debugRunId);
  assert.equal(run.status, 'paused');
  assert.equal(run.cursor, 1, 'paused after the first step');
  assert.ok(run.trace.steps[0].requests?.length, 'the trace carries the as-sent exchange');

  // Step over: the runs Skip verbatim — mark, then pump; the next step does not execute, its outputs are provided.
  await c.resumeDebugRun(wc.id, started.debugRunId, { action: { mode: 'Skip', skipOutputs: { forced: true } }, pause: { afterEachStep: true } });
  const over = await pump(started.debugRunId);
  const skipped = over.trace.steps.find((s) => s.stepId === 'adopt');
  assert.equal(skipped?.skipped, true, 'the stepped-over step did not execute');
  assert.deepEqual(skipped?.outputs, { forced: true });
  assert.equal(over.status, 'completed', 'skipping the final step completes the run');
  await assert.rejects(() => c.resumeDebugRun(wc.id, started.debugRunId, {}), (err) => err.status === 409, 'terminal runs refuse resume');

  const cancelled = await c.cancelDebugRun(wc.id, started.debugRunId);
  assert.equal(cancelled.status, 'cancelled');
});
