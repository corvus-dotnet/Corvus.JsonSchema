// In-memory mock of the Arazzo Control Plane API for the demo (and for tests).
//
// Returns a `fetch`-compatible function implementing all six operations with RFC 9457 errors and keyset
// pagination, so the kit is fully explorable with no server:
//
//   import { createMockControlPlane } from './mock-api.js';
//   import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';
//   const mock = createMockControlPlane();
//   const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

const TERMINAL = new Set(['Completed', 'Cancelled']);
let etagSeq = 1000;

function nextEtag() {
  return `"etag-${++etagSeq}"`;
}

function iso(offsetMs) {
  return new Date(Date.now() + offsetMs).toISOString();
}

function seedRuns() {
  const min = 60000;
  const hr = 60 * min;
  const day = 24 * hr;
  return [
    {
      id: 'run-7f3a9c21', workflowId: 'adopt-pet', status: 'Faulted', cursor: 2,
      createdAt: iso(-3 * hr), updatedAt: iso(-2 * min), etag: nextEtag(),
      fault: { stepId: 'reservePayment', attempt: 3, error: 'HttpRequestException: 502 from payments (upstream)', at: iso(-2 * min) },
      _errorType: 'HttpRequestException',
      correlationId: '7f3a9c21d4e54a1b9c0d1e2f3a4b5c6d', tags: ['tenant-42', 'priority'],
    },
    {
      id: 'run-1b88de40', workflowId: 'adopt-pet', status: 'Suspended', cursor: 4,
      createdAt: iso(-90 * min), updatedAt: iso(-30 * min), etag: nextEtag(),
      wait: { kind: 'Timer', dueAt: iso(45 * min) },
      correlationId: '1b88de40a1b2c3d4e5f60718293a4b5c', tags: ['tenant-42'],
    },
    {
      id: 'run-9c0142ab', workflowId: 'onboard-customer', status: 'Suspended', cursor: 1,
      createdAt: iso(-5 * hr), updatedAt: iso(-4 * hr), etag: nextEtag(),
      wait: { kind: 'Message', channel: 'kyc.results', correlationId: 'cust-55021' },
    },
    {
      id: 'run-33aa71f9', workflowId: 'onboard-customer', status: 'Running', cursor: 3,
      createdAt: iso(-8 * min), updatedAt: iso(-10000), etag: nextEtag(),
      correlationId: '33aa71f9b5c6d7e8f90a1b2c3d4e5f60', tags: ['tenant-7'],
    },
    {
      id: 'run-0a5512cd', workflowId: 'adopt-pet', status: 'Completed', cursor: 6,
      createdAt: iso(-2 * day), updatedAt: iso(-2 * day + 5 * min), etag: nextEtag(),
    },
    {
      id: 'run-44b0e7e2', workflowId: 'nightly-reconcile', status: 'Completed', cursor: 9,
      createdAt: iso(-9 * day), updatedAt: iso(-9 * day + 2 * min), etag: nextEtag(),
    },
    {
      id: 'run-6610ffac', workflowId: 'onboard-customer', status: 'Cancelled', cursor: 2,
      createdAt: iso(-40 * day), updatedAt: iso(-40 * day + min), etag: nextEtag(),
    },
    {
      id: 'run-2d77b410', workflowId: 'nightly-reconcile', status: 'Pending', cursor: 0,
      createdAt: iso(-20000), updatedAt: iso(-20000), etag: nextEtag(),
    },
  ];
}

function toSummary(run) {
  return {
    id: run.id,
    workflowId: run.workflowId,
    status: run.status,
    createdAt: run.createdAt,
    updatedAt: run.updatedAt,
    dueAt: run.wait?.kind === 'Timer' ? run.wait.dueAt : null,
    awaitingChannel: run.wait?.kind === 'Message' ? run.wait.channel : null,
    awaitingCorrelationId: run.wait?.kind === 'Message' ? (run.wait.correlationId ?? null) : null,
    errorType: run.status === 'Faulted' ? (run._errorType ?? 'Error') : null,
    correlationId: run.correlationId ?? null,
    tags: run.tags ?? [],
  };
}

function toDetail(run) {
  return {
    id: run.id,
    workflowId: run.workflowId,
    status: run.status,
    cursor: run.cursor,
    createdAt: run.createdAt,
    wait: run.wait ?? null,
    fault: run.fault ?? null,
    etag: run.etag,
    correlationId: run.correlationId ?? null,
    tags: run.tags ?? [],
  };
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function problem(status, title, detail) {
  return new Response(
    JSON.stringify({ type: 'about:blank', title, status, detail }),
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/**
 * @param {{ seed?: object[], latencyMs?: number }} [options]
 * @returns {{ fetch: (url: string, init?: RequestInit) => Promise<Response>, runs: object[] }}
 */
export function createMockControlPlane(options = {}) {
  const runs = options.seed ? structuredClone(options.seed) : seedRuns();
  const latency = options.latencyMs ?? 250;
  const find = (id) => runs.find((r) => r.id === id);

  async function handle(url, init = {}) {
    const method = (init.method || 'GET').toUpperCase();
    const u = new URL(url, 'https://mock');
    const path = u.pathname;
    const body = init.body ? JSON.parse(init.body) : undefined;

    // /runs collection
    if (/\/runs\/?$/.test(path)) {
      if (method === 'GET') return listRuns(u.searchParams);
      if (method === 'PURGE') return purgeRuns(u.searchParams);
      return problem(405, 'Method not allowed');
    }

    // /runs/{id}[/action]
    const m = path.match(/\/runs\/([^/]+)(?:\/(resume|cancel))?$/);
    if (m) {
      const id = decodeURIComponent(m[1]);
      const action = m[2];
      const run = find(id);
      if (!run) return problem(404, 'Run not found', `No run with id '${id}'.`);

      if (!action && method === 'GET') return json(toDetail(run));
      if (!action && method === 'DELETE') return deleteRun(run);
      if (action === 'resume' && method === 'POST') return resumeRun(run, body);
      if (action === 'cancel' && method === 'POST') return cancelRun(run, body);
      return problem(405, 'Method not allowed');
    }

    return problem(404, 'Not found', path);
  }

  function listRuns(params) {
    const status = params.get('status');
    const workflowId = params.get('workflowId');
    const limit = Math.max(1, Number(params.get('limit')) || 100);
    const offset = Number(atobSafe(params.get('pageToken'))) || 0;

    let filtered = [...runs].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));
    if (status) filtered = filtered.filter((r) => r.status === status);
    if (workflowId) filtered = filtered.filter((r) => r.workflowId.includes(workflowId));

    // Time-window filters: createdAfter/updatedAfter inclusive (>=), createdBefore/updatedBefore exclusive (<).
    const createdAfter = parseMs(params.get('createdAfter'));
    const createdBefore = parseMs(params.get('createdBefore'));
    const updatedAfter = parseMs(params.get('updatedAfter'));
    const updatedBefore = parseMs(params.get('updatedBefore'));
    if (createdAfter != null) filtered = filtered.filter((r) => Date.parse(r.createdAt) >= createdAfter);
    if (createdBefore != null) filtered = filtered.filter((r) => Date.parse(r.createdAt) < createdBefore);
    if (updatedAfter != null) filtered = filtered.filter((r) => Date.parse(r.updatedAt) >= updatedAfter);
    if (updatedBefore != null) filtered = filtered.filter((r) => Date.parse(r.updatedAt) < updatedBefore);

    // Tags are AND-matched (a run must carry every requested tag); correlationId is an exact match.
    const wantTags = params.getAll('tag').filter(Boolean);
    if (wantTags.length > 0) filtered = filtered.filter((r) => wantTags.every((t) => (r.tags ?? []).includes(t)));
    const correlationId = params.get('correlationId');
    if (correlationId) filtered = filtered.filter((r) => r.correlationId === correlationId);

    const slice = filtered.slice(offset, offset + limit);
    const hasMore = offset + limit < filtered.length;
    return json({
      runs: slice.map(toSummary),
      nextPageToken: hasMore ? btoaSafe(String(offset + limit)) : null,
    });
  }

  function resumeRun(run, request) {
    if (run.status !== 'Faulted') {
      return problem(409, 'Run is not faulted', `Run '${run.id}' is ${run.status}; only faulted runs can be resumed.`);
    }
    const mode = request?.mode;
    if (mode === 'Rewind' && typeof request.targetCursor === 'number') run.cursor = request.targetCursor;
    if (mode === 'Skip') run.cursor = (request.targetCursor ?? run.cursor + 1);
    run.fault = null;
    delete run._errorType;
    run.status = 'Running';
    run.updatedAt = iso(0);
    run.etag = nextEtag();
    return json(toDetail(run));
  }

  function cancelRun(run, request) {
    if (TERMINAL.has(run.status)) {
      return problem(409, 'Run already terminal', `Run '${run.id}' is ${run.status} and cannot be cancelled.`);
    }
    run.status = 'Cancelled';
    run.wait = null;
    run._cancelReason = request?.reason;
    run.updatedAt = iso(0);
    run.etag = nextEtag();
    return json(toDetail(run));
  }

  function deleteRun(run) {
    const i = runs.indexOf(run);
    runs.splice(i, 1);
    return new Response(null, { status: 204 });
  }

  function purgeRuns(params) {
    const olderThan = params.get('olderThan');
    const limit = Number(params.get('limit')) || Infinity;
    const cutoff = olderThan ? Date.parse(olderThan) : NaN;
    if (Number.isNaN(cutoff)) return problem(400, 'Invalid olderThan', 'olderThan must be an RFC 3339 timestamp.');
    let purged = 0;
    for (let i = runs.length - 1; i >= 0 && purged < limit; i--) {
      const r = runs[i];
      if (TERMINAL.has(r.status) && Date.parse(r.updatedAt) < cutoff) {
        runs.splice(i, 1);
        purged++;
      }
    }
    return json({ purgedCount: purged });
  }

  return {
    runs,
    fetch: async (url, init) => {
      if (latency) await new Promise((r) => setTimeout(r, latency));
      return handle(url, init);
    },
  };
}

function parseMs(value) {
  if (!value) return null;
  const ms = Date.parse(value);
  return Number.isNaN(ms) ? null : ms;
}

function btoaSafe(s) { return typeof btoa === 'function' ? btoa(s) : Buffer.from(s).toString('base64'); }
function atobSafe(s) { if (!s) return ''; return typeof atob === 'function' ? atob(s) : Buffer.from(s, 'base64').toString(); }
