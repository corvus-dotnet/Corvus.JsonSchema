// The kit's presentation of the server's readiness (ADR 0074): the reasons an entry is not ready, and who its usable
// credentials are restricted to. The rule itself is the server's; these only word its answer.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { notReadyReasons, restrictionSummary } from '../src/readiness.js';

const entry = (overrides) => ({ environment: 'production', ready: false, credentialsReady: false, evidenceRequired: false, sources: [], ...overrides });

test('a ready entry has no reasons', () => {
  assert.deepEqual(notReadyReasons(entry({ ready: true, credentialsReady: true, sources: [{ name: 'billing', usable: true }] })), []);
});

test('the reasons name the sources with no credential the runs may use, and the evidence the environment requires', () => {
  const reasons = notReadyReasons(entry({
    sources: [{ name: 'billing', usable: false }, { name: 'ledger', usable: true }, { name: 'kyc', usable: false }],
    evidenceRequired: true,
    evidenceGreen: false,
  }));
  assert.deepEqual(reasons, ['no usable credential for billing, kyc', 'this environment requires green publish evidence, which this version lacks']);
});

test('a draft, which has no evidence yet, is told it gains evidence when it is published', () => {
  const [reason] = notReadyReasons(entry({ credentialsReady: true, evidenceRequired: true }));
  assert.match(reason, /publish evidence, which a version gains when it is published/);
});

test('the restriction summary names who each usable credential is restricted to, and nothing for shared ones', () => {
  assert.equal(restrictionSummary(entry({ sources: [{ name: 'billing', usable: true }] })), '');
  assert.equal(
    restrictionSummary(entry({ sources: [
      { name: 'billing', usable: true, restriction: { kind: 'team', label: 'Payments' } },
      { name: 'ledger', usable: true, restriction: { kind: 'workflow' } },
      { name: 'kyc', usable: false },
    ] })),
    'billing: restricted to Payments (team); ledger: restricted to this workflow');
});
