// Example data for the demo — SEPARATE from the built-in seeds the tests assert against (the createMockControlPlane
// defaults in mock-api.js). Pass this to createMockControlPlane(...) from the demo; the tests keep the defaults, so the
// example data can evolve independently without breaking the gate.
//
// Multi-tenant correctness: tenant is the AMBIENT isolation boundary the deployment stamps (§14) — a user only ever
// sees their *current* tenant, so the product never surfaces a "tenant" constraint. Authorization inside a tenant is
// expressed in domains, teams, roles and classifications. (Switching tenants, for an identity in more than one, would be
// a separate console-level tenant switcher — see docs/control-plane/ux-review.md §7.) This example reflects that:
// nothing here is keyed by tenant.
import { seedRuns, adminGrant } from './mock-api.js';

const hr = 3600_000;
const day = 24 * hr;
const ago = (ms) => new Date(Date.now() - ms).toISOString();

// Reuse the rich run fixtures (they pair with the catalog for the resume walkthrough) but strip the tenant-* tags —
// you never label by tenant inside a single tenant's own view.
const runs = seedRuns().map((r) => ({ ...r, tags: (r.tags || []).filter((t) => !/^tenant-/i.test(t)) }));

// The reusable reach vocabulary (§14.2) — within-tenant concepts only: domain, classification, ABAC. No tenant rule.
const securityRules = [
  { name: 'shares-a-label', expression: '$claims.intersects', description: 'The principal shares at least one label with the row.', createdBy: 'system', createdAt: ago(40 * day), etag: 'etag-r1' },
  { name: 'abac-superset', expression: '$claims.superset', description: 'ABAC clearance: the principal holds every label the row carries.', createdBy: 'system', createdAt: ago(40 * day), etag: 'etag-r2' },
  { name: 'reach-payments', expression: "domain == 'payments'", description: 'Rows in the payments domain.', createdBy: 'priya@example.com', createdAt: ago(9 * day), lastUpdatedBy: 'priya@example.com', lastUpdatedAt: ago(2 * day), etag: 'etag-r3' },
  { name: 'reach-onboarding', expression: "domain == 'onboarding'", description: 'Rows in the onboarding domain.', createdBy: 'priya@example.com', createdAt: ago(7 * day), etag: 'etag-r4' },
  { name: 'data-confidential', expression: "classification <= 'confidential'", description: 'Rows classified Confidential or below.', createdBy: 'priya@example.com', createdAt: ago(3 * day), etag: 'etag-r5' },
];

// Claim → per-verb reach grants — keyed on team / role within the tenant; scoped via the reach-* rules above.
const securityBindings = [
  { id: 'bind-1', claimType: 'team', claimValue: 'payments', read: { ruleNames: ['reach-payments'] }, write: { ruleNames: ['reach-payments'] }, purge: { unrestricted: false }, order: 0, description: 'Payments team — read and write the payments domain.', createdBy: 'priya@example.com', createdAt: ago(12 * day), etag: 'etag-b1' },
  { id: 'bind-2', claimType: 'role', claimValue: 'sre', read: { unrestricted: true }, write: { ruleNames: ['reach-payments', 'reach-onboarding'] }, purge: { unrestricted: false }, order: 1, description: 'SRE — read everything; write the payments and onboarding domains.', createdBy: 'priya@example.com', createdAt: ago(5 * day), etag: 'etag-b2' },
  { id: 'bind-3', claimType: 'team', claimValue: 'growth', read: { ruleNames: ['reach-onboarding'] }, write: { unrestricted: false }, purge: { unrestricted: false }, order: 2, description: 'Growth team — read the onboarding domain.', createdBy: 'priya@example.com', createdAt: ago(4 * day), etag: 'etag-b3' },
];

// Resolvable grantees for the pickers — people, teams, a role, a workflow. No tenant grantee.
const grantees = [
  { kind: 'person', value: 'u-1042', label: 'Ada Lovelace', identity: [{ dimension: 'sys:iss', value: 'https://idp.example.com' }, { dimension: 'sys:sub', value: 'u-1042' }], source: 'directory', complete: true },
  { kind: 'person', value: 'u-2099', label: 'Grace Hopper', identity: [{ dimension: 'sys:sub', value: 'u-2099' }], source: 'observed', complete: false },
  { kind: 'team', value: 'payments', label: 'Payments', identity: [{ dimension: 'team', value: 'payments' }], source: 'directory', complete: true },
  { kind: 'team', value: 'growth', label: 'Growth', identity: [{ dimension: 'team', value: 'growth' }], source: 'directory', complete: true },
  { kind: 'role', value: 'sre', label: 'Site Reliability', identity: [{ dimension: 'role', value: 'sre' }], source: 'directory', complete: true },
  { kind: 'workflow', value: 'onboard-customer', label: 'onboard-customer', identity: [{ dimension: 'workflow', value: 'onboard-customer' }], source: 'observed', complete: true },
];

// Workflow administrators — teams, never tenant. adminGrant stamps the same stable digest the picker/remove round-trip uses.
const administrators = {
  'nightly-reconcile': [adminGrant([{ dimension: 'team', value: 'payments' }], 'team', 'Payments')],
  'onboard-customer': [
    adminGrant([{ dimension: 'team', value: 'growth' }], 'team', 'Growth'),
    adminGrant([{ dimension: 'team', value: 'platform' }], 'team', 'Platform'),
  ],
};

// Access requests — ONE pending (the approver-inbox star, cleared to inbox-zero in the walkthrough) plus history.
const accessRequests = [
  { id: 'req-2001', baseWorkflowId: 'payments-reconcile', requestedScopes: ['runs:read', 'runs:write'], subjectClaimType: 'preferred_username', subjectClaimValue: 'grace', requesterLabel: 'Grace Hopper', reason: 'On-call: re-run the reconcile after the ledger hotfix.', requestedDurationSeconds: 4 * 3600, status: 'Pending', createdBy: 'grace', createdAt: ago(3 * hr), etag: 'etag-a1' },
  { id: 'req-2002', baseWorkflowId: 'onboard-customer', requestedScopes: ['runs:read', 'runs:write'], subjectClaimType: 'preferred_username', subjectClaimValue: 'ada', requesterLabel: 'Ada Lovelace', reason: 'Investigating a stuck onboarding run.', status: 'Approved', createdBy: 'ada', createdAt: ago(26 * hr), decidedBy: 'priya@example.com', decidedAt: ago(25 * hr), grantedBindingId: 'bind-9001', grantedUntil: ago(-6 * hr), etag: 'etag-a2' },
  { id: 'req-2003', baseWorkflowId: 'nightly-reconcile', requestedScopes: ['runs:write'], subjectClaimType: 'preferred_username', subjectClaimValue: 'mallory', requesterLabel: 'Mallory Quinn', reason: 'Need to fix the overnight job.', status: 'Denied', createdBy: 'mallory', createdAt: ago(50 * hr), decidedBy: 'priya@example.com', decidedAt: ago(49 * hr), decisionReason: 'Use the read-only triage role instead.', etag: 'etag-a3' },
];

// The catalog, credentials and ordered dimensions use the mock's built-in defaults (already tenant-free).
export const DEMO_SEED = {
  seed: runs,
  securityRulesSeed: securityRules,
  securityBindingsSeed: securityBindings,
  granteesSeed: grantees,
  administratorsSeed: administrators,
  accessRequestsSeed: accessRequests,
};
