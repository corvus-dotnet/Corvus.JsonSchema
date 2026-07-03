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
  assert.equal(credentials.length, 5);
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
  assert.equal(seen.length, 5, 'all five bindings, exactly once');
  assert.equal(new Set(seen).size, 5, 'no duplicates across page boundaries');
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
  assert.ok(credentialUsage.some((u) => u.sourceName === 'billing' && u.environment === 'staging'), 'her usage-restricted credential is projected');
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
  assert.deepEqual(environments.map((e) => e.name), ['production', 'staging']);
  assert.equal(nextPageToken, null);
});

test('environments are reach-filtered — a reach-scoped caller sees only what its reach admits (non-disclosing)', async () => {
  const mock = createMockControlPlane({ latencyMs: 0 });
  const c = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });
  // Administrator (reach: all) sees every environment.
  mock.setPersona('administrator');
  assert.deepEqual((await c.listEnvironments()).environments.map((e) => e.name), ['production', 'staging']);
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
