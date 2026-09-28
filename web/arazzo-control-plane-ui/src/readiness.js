// Arazzo Control Plane — version readiness, as the server judges it (ADR 0074).
//
// A version is ready in an environment when every source it references has a credential there that its runs may
// use, judged as a run is (the credential's usage restriction against the version's identity, which only the server
// can see), and, where the environment requires evidence, its publish suite is green. The kit never approximates the
// rule: it asks the server (`listVersionReadiness`, or `evaluateDraftReadiness` for a version not yet published) and
// presents the answer. DOM-free, so it is shared by every surface that shows readiness.

/**
 * Every readiness entry for a catalogued version, across all pages, keyed by environment name.
 * @param {import('./arazzo-client.js').ArazzoControlPlaneClient} client
 * @param {string} baseWorkflowId
 * @param {number} versionNumber
 * @param {{ signal?: AbortSignal }} [opts]
 * @returns {Promise<Map<string, object>>} environment → an {@link EnvironmentReadiness}.
 */
export async function versionReadiness(client, baseWorkflowId, versionNumber, opts = {}) {
  const byEnvironment = new Map();
  for await (const page of client.listVersionReadinessPaged(baseWorkflowId, versionNumber, { limit: 200, signal: opts.signal })) {
    for (const entry of page.readiness) byEnvironment.set(entry.environment, entry);
  }
  return byEnvironment;
}

/**
 * Every readiness entry for a version of `baseWorkflowId` the caller would publish with `sources`, across all pages,
 * keyed by environment name. A draft has no evidence, so its entries carry no `evidenceGreen`.
 * @param {import('./arazzo-client.js').ArazzoControlPlaneClient} client
 * @param {string} baseWorkflowId
 * @param {string[]} sources
 * @param {{ signal?: AbortSignal }} [opts]
 * @returns {Promise<Map<string, object>>} environment → an {@link EnvironmentReadiness}.
 */
export async function draftReadiness(client, baseWorkflowId, sources, opts = {}) {
  const byEnvironment = new Map();
  for await (const page of client.evaluateDraftReadinessPaged(baseWorkflowId, sources, { limit: 200, signal: opts.signal })) {
    for (const entry of page.readiness) byEnvironment.set(entry.environment, entry);
  }
  return byEnvironment;
}

/**
 * Why an entry is not ready, one sentence per gate that refused it: the sources with no credential the version's runs
 * may use, and the evidence the environment requires. Empty when it is ready.
 * @param {object} entry An {@link EnvironmentReadiness}.
 * @returns {string[]}
 */
export function notReadyReasons(entry) {
  const reasons = [];
  const missing = (entry.sources ?? []).filter((s) => !s.usable).map((s) => s.name);
  if (missing.length) reasons.push(`no usable credential for ${missing.join(', ')}`);
  if (entry.evidenceRequired && entry.evidenceGreen !== true) {
    reasons.push(entry.evidenceGreen === false
      ? 'this environment requires green publish evidence, which this version lacks'
      : 'this environment requires green publish evidence, which a version gains when it is published');
  }
  return reasons;
}

/**
 * Who the version's usable credentials are restricted to, as one line (e.g. `billing: restricted to Payments (team)`),
 * or `''` when every usable credential is shared. The server names the grantee; it never describes the credential.
 * @param {object} entry An {@link EnvironmentReadiness}.
 * @returns {string}
 */
export function restrictionSummary(entry) {
  return (entry.sources ?? [])
    .filter((s) => s.usable && s.restriction)
    .map((s) => `${s.name}: restricted to ${granteeText(s.restriction)}`)
    .join('; ');
}

// A usable credential restricted to a workflow is restricted to this one (it would not be usable otherwise).
function granteeText(restriction) {
  if (restriction.kind === 'workflow' && !restriction.label) return 'this workflow';
  if (restriction.label && restriction.kind) return `${restriction.label} (${restriction.kind})`;
  return restriction.label || (restriction.kind ? `a ${restriction.kind}` : 'a named grantee');
}
