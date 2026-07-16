// Seed-data consistency: the mock's seeded world cross-checked through its own API. Every seeded
// reference must resolve — catalog workflow bindings against their referenced sources, run faults
// and waits against the workflow world, credentials against registered sources and environments,
// the GitHub tree against itself — so the demo never shows a state the real system could not reach.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createMockControlPlane } from '../demo/mock-api.js';

const mock = createMockControlPlane({ latencyMs: 0 });
const BASE = 'https://mock/arazzo/v1';
const get = async (p) => {
  const r = await mock.fetch(`${BASE}${p}`);
  if (!r.ok) return { __status: r.status };
  return r.json();
};
const items = (x) => x.items ?? x.versions ?? x.runs ?? x.sources ?? x.environments ?? x.credentials ?? x.workingCopies ?? x.scenarios ?? [];
const findings = [];
const flag = (area, msg) => findings.push(`[${area}] ${msg}`);

// ---- catalog: every version's workflow bindings must resolve in its referenced sources ----
const catalog = await get('/catalog?pageSize=100');
const versionsOf = async (base) => items(await get(`/catalog/${base}?pageSize=100`));
const opsAndChannelsOf = (doc) => {
  const ops = new Set(); const channels = new Set();
  for (const item of Object.values(doc.paths ?? {})) {
    for (const op of Object.values(item ?? {})) if (op?.operationId) ops.add(op.operationId);
  }
  for (const [key, ch] of Object.entries(doc.channels ?? {})) { channels.add(ch?.address ?? key); channels.add(key); }
  for (const op of Object.values(doc.operations ?? {})) if (op?.operationId) ops.add(op.operationId);
  return { ops, channels };
};

const allBases = [...new Set(items(catalog).map((v) => v.baseWorkflowId))];
for (const base of allBases) {
  for (const v of await versionsOf(base)) {
    const wf = await get(`/catalog/${base}/versions/${v.versionNumber}/workflow`);
    if (wf.__status) { flag('catalog', `${v.workflowId}: workflow doc not served (${wf.__status})`); continue; }
    const ops = new Set(); const channels = new Set();
    for (const ref of v.sources ?? []) {
      const src = await get(`/catalog/${base}/versions/${v.versionNumber}/sources/${ref.name}`);
      if (src.__status) { flag('catalog', `${v.workflowId}: referenced source '${ref.name}' not served (${src.__status})`); continue; }
      const found = opsAndChannelsOf(src);
      found.ops.forEach((o) => ops.add(o)); found.channels.forEach((c) => channels.add(c));
    }
    for (const w of wf.workflows ?? []) {
      for (const s of w.steps ?? []) {
        if (s.operationId && !ops.has(s.operationId)) flag('catalog', `${v.workflowId} step '${s.stepId}': operationId '${s.operationId}' not in referenced sources [${(v.sources ?? []).map((x) => x.name)}]`);
        if (s.channelPath && !channels.has(s.channelPath)) flag('catalog', `${v.workflowId} step '${s.stepId}': channel '${s.channelPath}' not in referenced sources`);
      }
    }
    // Evidence pathSummary steps must be real steps.
    const stepIds = new Set((wf.workflows ?? []).flatMap((w) => (w.steps ?? []).map((s) => s.stepId)));
    const detail = await get(`/catalog/${base}/versions/${v.versionNumber}`);
    for (const sc of detail.evidence?.scenarios ?? []) {
      for (const st of (sc.pathSummary ?? '').split('→').map((x) => x.trim()).filter(Boolean)) {
        if (!stepIds.has(st)) flag('evidence', `${v.workflowId} scenario '${sc.name}': pathSummary step '${st}' is not a step of the workflow`);
      }
    }
  }
}

// ---- runs: fault steps + wait channels must exist in the run's workflow world ----
const runs = await get('/runs?pageSize=100');
for (const r of items(runs)) {
  const base = r.workflowId.replace(/-v\d+$/, '');
  const n = Number(r.workflowId.match(/-v(\d+)$/)?.[1] ?? '1');
  const wf = await get(`/catalog/${base}/versions/${n}/workflow`);
  if (wf.__status) { flag('runs', `${r.id}: workflow ${r.workflowId} not in catalog (${wf.__status})`); continue; }
  const stepIds = new Set((wf.workflows ?? []).flatMap((w) => (w.steps ?? []).map((s) => s.stepId)));
  const detail = await get(`/runs/${r.id}`);
  if (detail.fault && !stepIds.has(detail.fault.stepId)) flag('runs', `${r.id}: fault.stepId '${detail.fault.stepId}' is not a step of ${r.workflowId}`);
  if (detail.wait?.channel) {
    const v = (await versionsOf(base)).find((x) => x.versionNumber === n);
    const channels = new Set();
    for (const ref of v?.sources ?? []) {
      const src = await get(`/catalog/${base}/versions/${n}/sources/${ref.name}`);
      if (!src.__status) opsAndChannelsOf(src).channels.forEach((c) => channels.add(c));
    }
    if (!channels.has(detail.wait.channel)) flag('runs', `${r.id}: waits on channel '${detail.wait.channel}' which no referenced source declares`);
  }
  if (detail.cursor > stepIds.size) flag('runs', `${r.id}: cursor ${detail.cursor} exceeds ${r.workflowId}'s ${stepIds.size} steps`);
}

// ---- credentials reference registered sources ----
const sources = await get('/sources?pageSize=100');
const sourceNames = new Set(items(sources).map((s) => s.name));
const creds = await get('/source-credentials?pageSize=100');
for (const c of items(creds)) {
  if (!sourceNames.has(c.sourceName)) flag('credentials', `${c.id}: references source '${c.sourceName}' which is not registered`);
}
const envs = await get('/environments?pageSize=100');
const envNames = new Set(items(envs).map((e) => e.name));
for (const c of items(creds)) {
  if (!envNames.has(c.environment)) flag('credentials', `${c.id}: references environment '${c.environment}' which does not exist`);
}
if (![...items(envs)].some((e) => e.requireEvidence)) flag('environments', 'no seeded environment demonstrates requireEvidence (§4.6 gate invisible in the demo)');

// ---- workspace: the fixture working copy ----
const wcs = await get('/workspace/workflows?pageSize=100');
for (const w of items(wcs)) {
  const wc = await get(`/workspace/workflows/${w.id}`);
  const scenarios = await get(`/workspace/workflows/${w.id}/scenarios`);
  if ((scenarios.scenarios ?? []).length === 0) flag('workspace', `working copy '${w.name}' has NO scenarios — the Scenarios tab demos empty`);
  const report = await get(`/workspace/workflows/${w.id}/validate`.replace('/validate', '/validate'));
  // validate is POST:
  const vr = await mock.fetch(`${BASE}/workspace/workflows/${w.id}/validate`, { method: 'POST' });
  const rep = await vr.json();
  for (const d of rep.diagnostics ?? []) flag('workspace', `'${w.name}' validate: [${d.severity}/${d.category}] ${d.instancePath}: ${d.message}`);
  if (!wc.gitBinding) flag('workspace', `working copy '${w.name}' has no gitBinding — pull/commit demo needs manual binding (informational)`);
}

// ---- github tree: parse + cross-reference ----
const begin = await (await mock.fetch(`${BASE}/github/auth`, { method: 'POST' })).json();
await mock.fetch(begin.authorizeUrl);
const root = await get('/github/repos/acme-org/specs/contents');
const treePaths = new Set();
const walk = async (path) => {
  const node = await get(`/github/repos/acme-org/specs/contents${path ? `?path=${encodeURIComponent(path)}` : ''}`);
  if (node.kind === 'dir') { for (const e of node.entries) { treePaths.add(e.path); if (e.type === 'dir') await walk(e.path); } }
};
await walk('');
for (const p of treePaths) {
  const node = await get(`/github/repos/acme-org/specs/contents?path=${encodeURIComponent(p)}`);
  if (node.kind === 'file') {
    try { JSON.parse(Buffer.from(node.file.content, 'base64').toString('utf8')); }
    catch { flag('github', `tree file '${p}' does not parse as JSON`); }
  }
}
if (![...treePaths].some((p) => p.includes('scenario'))) flag('github', 'tree has no scenarios directory — a bound pull cannot demonstrate scenario round-trip');
const adoptNode = await get('/github/repos/acme-org/specs/contents?path=flows/adopt.arazzo.json');
if (adoptNode.kind === 'file') {
  const doc = JSON.parse(Buffer.from(adoptNode.file.content, 'base64').toString('utf8'));
  for (const sd of doc.sourceDescriptions ?? []) {
    // the import demo should be able to find the spec beside the flow
    if (![...treePaths].some((p) => p.toLowerCase().includes(sd.name))) flag('github', `adopt flow declares source '${sd.name}' but the tree has no matching spec file`);
  }
  const branches = await get('/github/repos/acme-org/specs/branches');
  if (!(branches.branches ?? []).length) flag('github', 'no branches served');
}

test('the seeded demo world is internally consistent', () => {
  assert.deepEqual(findings, [], `seed inconsistencies:\n${findings.join('\n')}`);
});