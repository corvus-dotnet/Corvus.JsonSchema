// Tier 2 — contract conformance. Drives every client method through a capturing fetch and asserts the
// emitted request (method, path, query params, body) matches docs/control-plane/arazzo-control-plane.openapi.json,
// so the JS client cannot drift from the contract the generated .NET server/client are built against.
//   node --test test/conformance.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';

const BASE_PATH = '/arazzo/v1';
const doc = JSON.parse(readFileSync(new URL('../../../docs/control-plane/arazzo-control-plane.openapi.json', import.meta.url)));

/** Resolve a local $ref ("#/a/b/c") within the document. */
function deref(node) {
  let n = node;
  while (n && n.$ref) {
    n = n.$ref.replace(/^#\//, '').split('/').reduce((acc, k) => acc[k], doc);
  }
  return n;
}

/** Map each operationId → { method, path, queryParams: Set, hasBody }. Includes additionalOperations (PURGE). */
function indexOperations() {
  const ops = {};
  for (const [path, item] of Object.entries(doc.paths)) {
    const methodMap = { ...item };
    const additional = item.additionalOperations || {};
    for (const [method, op] of Object.entries(methodMap)) {
      if (method === 'additionalOperations' || method === 'parameters' || typeof op !== 'object' || !op.operationId) continue;
      ops[op.operationId] = collect(method, path, op);
    }
    for (const [method, op] of Object.entries(additional)) {
      if (op.operationId) ops[op.operationId] = collect(method, path, op);
    }
  }
  return ops;
}

function collect(method, path, op) {
  const queryParams = new Set();
  for (const p of op.parameters || []) {
    const param = deref(p);
    if (param.in === 'query') queryParams.add(param.name);
  }
  return { method: method.toUpperCase(), path, queryParams, hasBody: !!op.requestBody };
}

const OPS = indexOperations();

/** A client whose fetch records the request and returns a shape each client method can parse. */
function capturing() {
  const calls = [];
  const fetch = async (url, init = {}) => {
    const u = new URL(url);
    const method = (init.method || 'GET').toUpperCase();
    calls.push({ method, path: u.pathname.replace(BASE_PATH, ''), query: u.searchParams, body: init.body ? JSON.parse(init.body) : undefined });
    if (method === 'DELETE') return new Response(null, { status: 204 });
    if (/\/runs\/?$/.test(u.pathname) && method === 'PURGE') return json({ purgedCount: 0 });
    if (/\/runs\/?$/.test(u.pathname)) return json({ runs: [], nextPageToken: null });
    return json({ id: 'r1', workflowId: 'w', status: 'Running', cursor: 0, createdAt: new Date(0).toISOString(), etag: '"1"' });
  };
  return { client: new ArazzoControlPlaneClient({ baseUrl: `https://mock${BASE_PATH}`, fetch }), calls };
}

const json = (body) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });

test('the contract declares all six operations', () => {
  for (const id of ['listRuns', 'getRun', 'resumeRun', 'cancelRun', 'deleteRun', 'purgeRuns']) {
    assert.ok(OPS[id], `operation ${id} present in the OpenAPI document`);
  }
});

test('listRuns: method, path, and every emitted query param is declared by the contract', async () => {
  const { client, calls } = capturing();
  await client.listRuns({
    status: 'Faulted', workflowId: 'w',
    createdAfter: '2026-01-01T00:00:00Z', createdBefore: '2026-02-01T00:00:00Z',
    updatedAfter: '2026-01-15T00:00:00Z', updatedBefore: '2026-01-20T00:00:00Z',
    limit: 50, pageToken: 'tok',
  });
  const call = calls[0];
  assert.equal(call.method, OPS.listRuns.method);
  assert.equal(call.path, OPS.listRuns.path);
  for (const key of call.query.keys()) {
    assert.ok(OPS.listRuns.queryParams.has(key), `query param '${key}' is declared in the contract`);
  }
});

test('listRuns: the time-window params exist in the contract and the client emits each', async () => {
  for (const p of ['createdAfter', 'createdBefore', 'updatedAfter', 'updatedBefore']) {
    assert.ok(OPS.listRuns.queryParams.has(p), `contract declares ${p}`);
  }
  const { client, calls } = capturing();
  // String instants pass through unchanged (only Date objects are normalised via toISOString()).
  await client.listRuns({ createdAfter: '2026-01-01T00:00:00Z', updatedBefore: '2026-02-01T00:00:00Z' });
  assert.equal(calls[0].query.get('createdAfter'), '2026-01-01T00:00:00Z');
  assert.equal(calls[0].query.get('updatedBefore'), '2026-02-01T00:00:00Z');
  // A Date is normalised to an ISO instant.
  const c2 = capturing();
  await c2.client.listRuns({ createdAfter: new Date('2026-03-01T00:00:00Z') });
  assert.equal(c2.calls[0].query.get('createdAfter'), '2026-03-01T00:00:00.000Z');
});

test('listRuns: tag (repeatable, AND) + correlationId exist in the contract and the client emits them', async () => {
  assert.ok(OPS.listRuns.queryParams.has('tag'), 'contract declares tag');
  assert.ok(OPS.listRuns.queryParams.has('correlationId'), 'contract declares correlationId');
  const { client, calls } = capturing();
  await client.listRuns({ tags: ['tenant-42', 'priority'], correlationId: 'trace-abc' });
  // Each tag is emitted as a repeated `tag` param (form/explode), AND-matched server-side.
  assert.deepEqual(calls[0].query.getAll('tag'), ['tenant-42', 'priority']);
  assert.equal(calls[0].query.get('correlationId'), 'trace-abc');
});

test('getRun / deleteRun: method + templated path', async () => {
  const { client, calls } = capturing();
  await client.getRun('run-1');
  assert.equal(calls[0].method, OPS.getRun.method);
  assert.equal(calls[0].path, OPS.getRun.path.replace('{runId}', 'run-1'));

  await client.deleteRun('run-1');
  assert.equal(calls[1].method, OPS.deleteRun.method);
  assert.equal(calls[1].path, OPS.deleteRun.path.replace('{runId}', 'run-1'));
});

test('resumeRun: POST the templated path with a body whose mode is a documented ResumeRequest variant', async () => {
  const variants = doc.components.schemas.ResumeRequest.oneOf
    .map((r) => deref(r).properties.mode.const)
    .filter(Boolean);
  assert.deepEqual(new Set(variants), new Set(['RetryFaultedStep', 'Rewind', 'Skip', 'StatePatch']));

  const { client, calls } = capturing();
  await client.resumeRun('run-1', { mode: 'Rewind', targetCursor: 3 });
  assert.equal(calls[0].method, OPS.resumeRun.method);
  assert.equal(calls[0].path, OPS.resumeRun.path.replace('{runId}', 'run-1'));
  assert.ok(variants.includes(calls[0].body.mode), 'body.mode is a documented variant');
});

test('cancelRun: POST the templated path with a CancelRequest body', async () => {
  const { client, calls } = capturing();
  await client.cancelRun('run-1', { reason: 'because' });
  assert.equal(calls[0].method, OPS.cancelRun.method);
  assert.equal(calls[0].path, OPS.cancelRun.path.replace('{runId}', 'run-1'));
  assert.equal(calls[0].body.reason, 'because');
});

test('purgeRuns: uses the non-standard PURGE method on the collection with documented query params', async () => {
  assert.equal(OPS.purgeRuns.method, 'PURGE');
  const { client, calls } = capturing();
  await client.purgeRuns({ olderThan: '2026-01-01T00:00:00Z', limit: 10 });
  assert.equal(calls[0].method, 'PURGE');
  assert.equal(calls[0].path, OPS.purgeRuns.path);
  for (const key of calls[0].query.keys()) {
    assert.ok(OPS.purgeRuns.queryParams.has(key), `purge query param '${key}' is declared`);
  }
});

test('the contract declares the credential and administration operations', () => {
  for (const id of ['listCredentials', 'getCredential', 'createCredential', 'updateCredential', 'deleteCredential',
                    'listAdministrators', 'addAdministrator', 'removeAdministrator', 'transferAdministration']) {
    assert.ok(OPS[id], `operation ${id} present in the OpenAPI document`);
  }
});

test('credentials: each client method emits the contract method + templated path + body', async () => {
  const { client, calls } = capturing();
  await client.listCredentials({ limit: 25, pageToken: 'tok' });
  assert.equal(calls[0].method, OPS.listCredentials.method);
  assert.equal(calls[0].path, OPS.listCredentials.path);
  for (const key of calls[0].query.keys()) {
    assert.ok(OPS.listCredentials.queryParams.has(key), `credentials query param '${key}' is declared in the contract`);
  }

  await client.getCredential('petstore', 'production');
  assert.equal(calls[1].method, OPS.getCredential.method);
  assert.equal(calls[1].path, OPS.getCredential.path.replace('{sourceName}', 'petstore').replace('{environment}', 'production'));

  await client.createCredential({ sourceName: 'a', environment: 'b', authKind: 'apiKey', secretRefs: [{ name: 'value', ref: 'env://A' }] });
  assert.equal(calls[2].method, OPS.createCredential.method);
  assert.equal(calls[2].path, OPS.createCredential.path);
  assert.equal(calls[2].body.sourceName, 'a');

  await client.updateCredential('petstore', 'production', { authKind: 'bearer', secretRefs: [{ name: 'value', ref: 'env://B' }] });
  assert.equal(calls[3].method, OPS.updateCredential.method);
  assert.equal(calls[3].path, OPS.updateCredential.path.replace('{sourceName}', 'petstore').replace('{environment}', 'production'));

  await client.deleteCredential('petstore', 'production');
  assert.equal(calls[4].method, OPS.deleteCredential.method);
  assert.equal(calls[4].path, OPS.deleteCredential.path.replace('{sourceName}', 'petstore').replace('{environment}', 'production'));
});

test('administrators: each client method emits the contract method + templated path + body', async () => {
  const { client, calls } = capturing();
  await client.listAdministrators('flow');
  assert.equal(calls[0].method, OPS.listAdministrators.method);
  assert.equal(calls[0].path, OPS.listAdministrators.path.replace('{baseWorkflowId}', 'flow'));

  await client.addAdministrator('flow', { dimension: 'tenant', value: 'acme' });
  assert.equal(calls[1].method, OPS.addAdministrator.method);
  assert.equal(calls[1].path, OPS.addAdministrator.path.replace('{baseWorkflowId}', 'flow'));
  assert.equal(calls[1].body.dimension, 'tenant');

  await client.removeAdministrator('flow', 'tenant', 'acme');
  assert.equal(calls[2].method, OPS.removeAdministrator.method);
  assert.equal(calls[2].path, OPS.removeAdministrator.path.replace('{baseWorkflowId}', 'flow').replace('{dimension}', 'tenant').replace('{value}', 'acme'));

  await client.transferAdministration('flow', { administrators: [{ dimension: 'tenant', value: 'acme' }] });
  assert.equal(calls[3].method, OPS.transferAdministration.method);
  assert.equal(calls[3].path, OPS.transferAdministration.path.replace('{baseWorkflowId}', 'flow'));
  assert.equal(calls[3].body.administrators[0].value, 'acme');
});

test('the contract declares the access-request operations', () => {
  for (const id of ['submitAccessRequest', 'listAccessRequests', 'getAccessRequest', 'approveAccessRequest',
                    'approveAccessRequestAsEligible', 'denyAccessRequest', 'withdrawAccessRequest', 'revokeAccessRequest']) {
    assert.ok(OPS[id], `operation ${id} present in the OpenAPI document`);
  }
});

test('access requests: each client method emits the contract method + templated path + body', async () => {
  const { client, calls } = capturing();
  await client.submitAccessRequest({ baseWorkflowId: 'flow', requestedScopes: ['runs:write'], reason: 'on-call', requestedDurationSeconds: 3600 });
  assert.equal(calls[0].method, OPS.submitAccessRequest.method);
  assert.equal(calls[0].path, OPS.submitAccessRequest.path);
  assert.deepEqual(calls[0].body.requestedScopes, ['runs:write']);
  assert.equal(calls[0].body.requestedDurationSeconds, 3600);

  await client.listAccessRequests({ status: 'Pending', baseWorkflowId: 'flow' });
  assert.equal(calls[1].method, OPS.listAccessRequests.method);
  assert.equal(calls[1].path, OPS.listAccessRequests.path);
  for (const key of calls[1].query.keys()) {
    assert.ok(OPS.listAccessRequests.queryParams.has(key), `query param '${key}' is declared`);
  }

  await client.getAccessRequest('req-1');
  assert.equal(calls[2].method, OPS.getAccessRequest.method);
  assert.equal(calls[2].path, OPS.getAccessRequest.path.replace('{requestId}', 'req-1'));

  await client.approveAccessRequest('req-1', { reason: 'ok' });
  assert.equal(calls[3].method, OPS.approveAccessRequest.method);
  assert.equal(calls[3].path, OPS.approveAccessRequest.path.replace('{requestId}', 'req-1'));
  assert.equal(calls[3].body.reason, 'ok');

  await client.approveAccessRequestAsEligible('req-1', { reason: 'jit', eligibilityWindowSeconds: 86400 });
  assert.equal(calls[4].method, OPS.approveAccessRequestAsEligible.method);
  assert.equal(calls[4].path, OPS.approveAccessRequestAsEligible.path.replace('{requestId}', 'req-1'));
  assert.equal(calls[4].body.eligibilityWindowSeconds, 86400);

  await client.denyAccessRequest('req-1', { reason: 'no' });
  assert.equal(calls[5].method, OPS.denyAccessRequest.method);
  assert.equal(calls[5].path, OPS.denyAccessRequest.path.replace('{requestId}', 'req-1'));

  await client.withdrawAccessRequest('req-1');
  assert.equal(calls[6].method, OPS.withdrawAccessRequest.method);
  assert.equal(calls[6].path, OPS.withdrawAccessRequest.path.replace('{requestId}', 'req-1'));

  await client.revokeAccessRequest('req-1');
  assert.equal(calls[7].method, OPS.revokeAccessRequest.method);
  assert.equal(calls[7].path, OPS.revokeAccessRequest.path.replace('{requestId}', 'req-1'));
});
