// Tier 1 — the execution budget's vocabulary (ADR 0068): how the six limits are written and read, the platform's
// fixed fault types, the one place a run's resumability is decided, and the demo mock resolving a budget by the
// server's rule, which the component and UX tiers stand on.
//   node --test test/execution-budget.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { BUDGET_LIMITS, FAULT_TYPES, describeFault, formatBytes, formatLimit, formatSeconds, parseLimit, resumability, sameLimits } from '../src/execution-budget.js';
import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';
import { BUDGET_CEILING, createMockControlPlane, resolveBudget } from '../demo/mock-api.js';
import { readFileSync } from 'node:fs';

const doc = JSON.parse(readFileSync(new URL('../../../docs/arazzo/reference/arazzo-control-plane.openapi.json', import.meta.url)));
const limit = (key) => BUDGET_LIMITS.find((l) => l.key === key);
const client = () => new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: createMockControlPlane({ latencyMs: 0 }).fetch });

test('the six limits are exactly the limits the contract names', () => {
  assert.deepEqual(BUDGET_LIMITS.map((l) => l.key).sort(), Object.keys(doc.components.schemas.ResolvedExecutionBudget.properties).sort());
  assert.deepEqual(BUDGET_LIMITS.map((l) => l.key).sort(), Object.keys(doc.components.schemas.ExecutionBudget.properties).sort());
});

test('a limit is written as its exact number, with a readable form only when there is a clean one', () => {
  assert.equal(formatSeconds(43200), '43200s (12h)');
  assert.equal(formatSeconds(172800), '172800s (2d)');
  assert.equal(formatSeconds(90), '90s');
  assert.equal(formatSeconds(120), '120s (2m)');
  assert.equal(formatBytes(16 * 1024 * 1024), '16,777,216 (16 MiB)');
  assert.equal(formatBytes(2048), '2,048 (2 KiB)');
  assert.equal(formatBytes(1500), '1,500');
  assert.equal(formatLimit(limit('maxSteps'), { maxSteps: 250 }), '250');
  assert.equal(formatLimit(limit('maxSteps'), {}), '—');
  assert.equal(formatLimit(limit('maxSteps'), undefined), '—');
});

test('an override input reads as a limit, as nothing, or as why it cannot stand', () => {
  const ceiling = { maxSteps: 250, maxSubWorkflowDepth: 8 };
  assert.deepEqual(parseLimit(limit('maxSteps'), '25', ceiling), { value: 25 });
  assert.deepEqual(parseLimit(limit('maxSteps'), '  ', ceiling), {});
  assert.deepEqual(parseLimit(limit('maxSteps'), '250', ceiling), { value: 250 });
  assert.match(parseLimit(limit('maxSteps'), '251', ceiling).error, /ceiling of 250/);
  assert.match(parseLimit(limit('maxSteps'), '0', ceiling).error, /at least 1/);
  assert.deepEqual(parseLimit(limit('maxSubWorkflowDepth'), '0', ceiling), { value: 0 });
  for (const bad of ['-3', '+3', '1.5', '1e3', 'lots', '3 ']) {
    const parsed = parseLimit(limit('maxSteps'), bad, ceiling);
    if (bad.trim() === '3') assert.deepEqual(parsed, { value: 3 }); else assert.match(parsed.error, /whole number/);
  }
  // With no ceiling known (the create dialog before any environment was opened) the server decides.
  assert.deepEqual(parseLimit(limit('maxSteps'), '9999'), { value: 9999 });
});

test('overrides are compared by the limits they name', () => {
  assert.equal(sameLimits({ maxSteps: 3 }, { maxSteps: 3 }), true);
  assert.equal(sameLimits({}, undefined), true);
  assert.equal(sameLimits({ maxSteps: 3 }, {}), false);
  assert.equal(sameLimits({ maxSteps: 3 }, { maxSteps: 3, stepTimeoutSeconds: 7 }), false);
});

test('the six fixed fault types are described, and a steps own failure is not', () => {
  assert.deepEqual(Object.keys(FAULT_TYPES).sort(), ['budget-deadline', 'budget-depth', 'budget-fuel', 'executor-unhandled', 'executor-unresolvable', 'transport-unbound']);
  for (const [error, described] of Object.entries(FAULT_TYPES)) {
    assert.ok(described.meaning && described.remedy, error);
    assert.equal(described.budget, error.startsWith('budget-'));

    // The contract documents the same values on the fault's error.
    assert.ok(doc.components.schemas.WorkflowFault.properties.error.description.includes('`' + error + '`'), `${error} is documented in the contract`);
  }
  assert.equal(describeFault('HttpRequestException: 502'), null);
  assert.equal(describeFault('constructor'), null);
  assert.equal(describeFault(undefined), null);
});

test('a budget-faulted run is resumable exactly when the server says a re-budget would let it run', () => {
  const faulted = (error, extra = {}) => ({ status: 'Faulted', fault: { error }, ...extra });
  assert.deepEqual(resumability(faulted('boom')), { resumable: true });
  assert.deepEqual(resumability(faulted('executor-unresolvable')), { resumable: true });
  assert.deepEqual(resumability(faulted('budget-fuel', { rebudget: { resumable: true } })), { resumable: true });
  assert.equal(resumability(faulted('budget-fuel', { rebudget: { resumable: false } })).resumable, false);
  assert.match(resumability(faulted('budget-fuel', { rebudget: { resumable: false } })).reason, /Raise its environment's limit, or re-run/);
  assert.match(resumability(faulted('budget-deadline')).reason, /no budget to resolve again/);
  assert.equal(resumability({ status: 'Completed' }).resumable, false);
});

test('the mock resolves a budget by the servers rule: the ceiling, tightened limit by limit, never widened', () => {
  assert.deepEqual(resolveBudget(undefined), { ...BUDGET_CEILING });
  assert.equal(resolveBudget({ maxSteps: 25 }).maxSteps, 25);
  assert.equal(resolveBudget({ maxSteps: 25 }).wallClockSeconds, BUDGET_CEILING.wallClockSeconds);
  assert.equal(resolveBudget({ maxSteps: 9999 }).maxSteps, BUDGET_CEILING.maxSteps);
});

test('an environments budget is read whole, authored on create and update, replaced whole, and cleared by an empty override', async () => {
  const c = client();
  const production = await c.getEnvironmentExecutionBudget('production');
  assert.deepEqual(production.override, { maxSteps: 120, stepTimeoutSeconds: 30 });
  assert.equal(production.effective.maxSteps, 120);
  assert.equal(production.effective.wallClockSeconds, BUDGET_CEILING.wallClockSeconds);
  assert.deepEqual(production.ceiling, { ...BUDGET_CEILING });

  const uat = await c.getEnvironmentExecutionBudget('uat');
  assert.equal(uat.override, undefined);
  assert.deepEqual(uat.effective, { ...BUDGET_CEILING });

  await c.createEnvironment({ name: 'qa', executionBudget: { maxSteps: 10 } });
  assert.deepEqual((await c.getEnvironmentExecutionBudget('qa')).override, { maxSteps: 10 });

  await c.updateEnvironment('qa', { executionBudget: { stepTimeoutSeconds: 5 } });
  assert.deepEqual((await c.getEnvironmentExecutionBudget('qa')).override, { stepTimeoutSeconds: 5 });

  await c.updateEnvironment('qa', { displayName: 'QA' });
  assert.deepEqual((await c.getEnvironmentExecutionBudget('qa')).override, { stepTimeoutSeconds: 5 });

  await c.updateEnvironment('qa', { executionBudget: {} });
  assert.equal((await c.getEnvironmentExecutionBudget('qa')).override, undefined);

  await assert.rejects(c.updateEnvironment('qa', { executionBudget: { maxSteps: BUDGET_CEILING.maxSteps + 1 } }), (err) => err.status === 400 && /maxSteps/.test(err.problem.detail));
  await assert.rejects(c.getEnvironmentExecutionBudget('nowhere'), (err) => err.status === 404);
});

test('a budget-faulted run says whether a resume would run, resumes by being re-budgeted, and is refused while it would not', async () => {
  const c = client();
  const resumable = await c.getRun('run-b0d9e701');
  assert.equal(resumable.budget.maxSteps, 3);
  assert.deepEqual(resumable.rebudget, { effective: { ...BUDGET_CEILING, maxSteps: 120, stepTimeoutSeconds: 30 }, resumable: true });

  const stuck = await c.getRun('run-b0d9e702');
  assert.equal(stuck.rebudget.resumable, false);
  await assert.rejects(c.resumeRun('run-b0d9e702', { mode: 'RetryFaultedStep' }), (err) => err.status === 409 && /re-run/.test(err.problem.detail));

  // Tightening production below what the run has used turns the answer, which is computed per read.
  await c.updateEnvironment('production', { executionBudget: { maxSteps: 3 } });
  assert.equal((await c.getRun('run-b0d9e701')).rebudget.resumable, false);
  await c.updateEnvironment('production', { executionBudget: { maxSteps: 120, stepTimeoutSeconds: 30 } });

  const resumed = await c.resumeRun('run-b0d9e701', { mode: 'RetryFaultedStep' });
  assert.equal(resumed.status, 'Running');
  assert.equal(resumed.budget.maxSteps, 120);
  assert.equal(resumed.rebudget, undefined);

  // A run that did not fault on its budget carries no rebudget.
  assert.equal((await c.getRun('run-7f3a9c21')).rebudget, undefined);
});

test('a re-run is a new pending run of the same version, environment and tags, naming the original', async () => {
  const c = client();
  const accepted = await c.rerunRun('run-b0d9e702', { idempotencyKey: 'k1' });
  assert.equal(accepted.status, 'Pending');
  assert.equal(accepted.workflowId, 'onboard-customer-v1');
  assert.notEqual(accepted.runId, 'run-b0d9e702');

  const rerun = await c.getRun(accepted.runId);
  assert.equal(rerun.rerunOf, 'run-b0d9e702');
  assert.equal(rerun.environment, 'staging');
  assert.deepEqual(rerun.tags, ['tenant-7']);

  // A fresh budget, from staging as it is now, and not the one the original was frozen with.
  assert.equal(rerun.budget.wallClockSeconds, 3600);
  assert.notEqual(rerun.correlationId, 'b0d9e7020000000000000000000000a2');
  await assert.rejects(c.rerunRun('run-nope'), (err) => err.status === 404);
  assert.throws(() => c.rerunRun(''), TypeError);
});
