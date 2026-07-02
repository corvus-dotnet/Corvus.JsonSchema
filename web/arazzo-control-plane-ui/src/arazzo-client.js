// Arazzo Control Plane — Layer 0 API client (no DOM).
//
// A dependency-free ES module wrapping the six control-plane operations described by
// docs/control-plane/arazzo-control-plane.openapi.json. Usable in the browser, Node, tests, or a CLI.
//
//   import { ArazzoControlPlaneClient, ProblemError } from './arazzo-client.js';
//   const client = new ArazzoControlPlaneClient({ baseUrl: '/arazzo/v1', getAuthHeader: () => `Bearer ${token}` });
//   const { runs, nextPageToken } = await client.listRuns({ status: 'Faulted' });
//
// See docs/control-plane/ui-design.md (Layer 0) for the contract.

/**
 * The valid {@link WorkflowRunStatus} values, in lifecycle-ish order.
 * @type {ReadonlyArray<string>}
 */
export const RUN_STATUSES = Object.freeze([
  'Pending',
  'Running',
  'Suspended',
  'Completed',
  'Cancelled',
  'Faulted',
]);

/** Resume modes accepted by {@link ArazzoControlPlaneClient#resumeRun}. */
export const RESUME_MODES = Object.freeze([
  'RetryFaultedStep',
  'Rewind',
  'Skip',
  'StatePatch',
]);

/** The valid catalog version lifecycle statuses. */
export const CATALOG_STATUSES = Object.freeze(['Active', 'Obsolete']);

/**
 * An error carrying an RFC 9457 `application/problem+json` body returned by the control plane. Widgets
 * branch on {@link ProblemError#status} (notably `404` and `409`) rather than parsing messages.
 */
export class ProblemError extends Error {
  /**
   * @param {number} status HTTP status code.
   * @param {object} [problem] The parsed problem document (`{ type, title, status, detail, instance }`).
   * @param {Response} [response] The originating response, when available.
   */
  constructor(status, problem = {}, response = undefined) {
    super(problem.title || problem.detail || `Control plane request failed (${status})`);
    this.name = 'ProblemError';
    this.status = status;
    /** @type {string|undefined} */ this.type = problem.type;
    /** @type {string|undefined} */ this.title = problem.title;
    /** @type {string|undefined} */ this.detail = problem.detail;
    /** @type {string|undefined} */ this.instance = problem.instance;
    /** @type {object} */ this.problem = problem;
    /** @type {Response|undefined} */ this.response = response;
  }
}

/**
 * @typedef {object} ArazzoControlPlaneClientOptions
 * @property {string} baseUrl Base URL of the control-plane API, e.g. `/arazzo/v1` (trailing slash optional).
 * @property {(input: string, init: RequestInit) => Promise<Response>} [fetch] A `fetch`-compatible function;
 *   the place to add interceptors/retries/an mTLS host. Takes precedence over `getAuthHeader`/`credentials`.
 * @property {() => (string | Promise<string>)} [getAuthHeader] Returns the value for the `Authorization`
 *   header (e.g. `Bearer …`) on each request. Used when `fetch` is not supplied.
 * @property {RequestCredentials} [credentials] Passed to `fetch` when neither `fetch` nor `getAuthHeader` is
 *   set — defaults to `'include'` so a same-origin reverse proxy (cookie/mTLS) authenticates the call.
 * @property {AbortSignal} [signal] A default abort signal applied to every request.
 */

/**
 * A thin, typed client over the Arazzo control-plane REST API. Construct one per host and share it; the
 * Layer-1 components accept it via their `.client` property.
 */
export class ArazzoControlPlaneClient {
  /** @param {ArazzoControlPlaneClientOptions} options */
  constructor(options) {
    if (!options || !options.baseUrl) {
      throw new TypeError('ArazzoControlPlaneClient requires a baseUrl.');
    }

    /** @private */ this._baseUrl = String(options.baseUrl).replace(/\/+$/, '');
    /** @private */ this._fetch = options.fetch;
    /** @private */ this._getAuthHeader = options.getAuthHeader;
    /** @private */ this._credentials = options.credentials ?? 'include';
    /** @private */ this._signal = options.signal;
  }

  // ---- runs:read --------------------------------------------------------------------------------

  /**
   * `listRuns` — one page of the run visibility index. The time-window filters take RFC 3339 / ISO 8601
   * instants (or `Date`s): `createdAfter`/`updatedAfter` are inclusive, `createdBefore`/`updatedBefore`
   * exclusive.
   * @param {{ status?: string, workflowId?: string, createdAfter?: (string|Date), createdBefore?: (string|Date), updatedAfter?: (string|Date), updatedBefore?: (string|Date), tags?: string[], correlationId?: string, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   *   `tags` are AND-matched (a run must carry all of them); `correlationId` is an exact match.
   * @returns {Promise<{ runs: object[], nextPageToken: (string|null) }>} A {@link WorkflowRunPage}.
   */
  async listRuns(query = {}) {
    const search = new URLSearchParams();
    if (query.status) search.set('status', query.status);
    if (query.workflowId) search.set('workflowId', query.workflowId);
    if (query.createdAfter) search.set('createdAfter', toInstant(query.createdAfter));
    if (query.createdBefore) search.set('createdBefore', toInstant(query.createdBefore));
    if (query.updatedAfter) search.set('updatedAfter', toInstant(query.updatedAfter));
    if (query.updatedBefore) search.set('updatedBefore', toInstant(query.updatedBefore));
    for (const tag of query.tags ?? []) {
      if (tag) search.append('tag', tag);
    }
    if (query.correlationId) search.set('correlationId', query.correlationId);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const page = await this._request('GET', `/runs${qs(search)}`, { signal: query.signal });
    return { runs: page.runs ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `listRuns`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ status?: string, workflowId?: string, limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ runs: object[], nextPageToken: (string|null) }>}
   */
  async *listRunsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listRuns({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getRun` — full detail for one run.
   * @param {string} runId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} A {@link WorkflowRunDetail}. Throws {@link ProblemError} with `status === 404`
   *   if the run is unknown.
   */
  getRun(runId, opts = {}) {
    return this._request('GET', `/runs/${encodeURIComponent(runId)}`, { signal: opts.signal });
  }

  // ---- runs:write -------------------------------------------------------------------------------

  /**
   * `resumeRun` — resume a faulted run.
   * @param {string} runId
   * @param {object} resume One of the resume requests, selected by `mode`:
   *   `{ mode: 'RetryFaultedStep' }` ·
   *   `{ mode: 'Rewind', targetCursor }` ·
   *   `{ mode: 'Skip', targetCursor?, skipOutputs? }` ·
   *   `{ mode: 'StatePatch', patch: [...] }` (RFC 6902).
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The run's new {@link WorkflowRunDetail}. Throws {@link ProblemError} with
   *   `status === 409` if the run is not faulted / changed concurrently / the patch failed, or `404`.
   */
  resumeRun(runId, resume, opts = {}) {
    if (!resume || !RESUME_MODES.includes(resume.mode)) {
      throw new TypeError(`resumeRun requires a mode of one of: ${RESUME_MODES.join(', ')}.`);
    }
    return this._request('POST', `/runs/${encodeURIComponent(runId)}/resume`, { body: resume, signal: opts.signal });
  }

  /**
   * `cancelRun` — mark a non-terminal run `Cancelled`.
   * @param {string} runId
   * @param {{ reason?: string }} [request]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The run's new {@link WorkflowRunDetail}. Throws {@link ProblemError} with
   *   `status === 409` if already terminal/held.
   */
  cancelRun(runId, request = {}, opts = {}) {
    return this._request('POST', `/runs/${encodeURIComponent(runId)}/cancel`, { body: request, signal: opts.signal });
  }

  // ---- runs:purge -------------------------------------------------------------------------------

  /**
   * `deleteRun` — permanently delete one run by id.
   * @param {string} runId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>} Resolves on `204`. Throws {@link ProblemError} with `status === 404`/`409`.
   */
  async deleteRun(runId, opts = {}) {
    await this._request('DELETE', `/runs/${encodeURIComponent(runId)}`, { signal: opts.signal });
  }

  /**
   * `purgeRuns` — bulk-reap completed/cancelled runs older than `olderThan`.
   * @param {{ olderThan: string, limit?: number, signal?: AbortSignal }} request `olderThan` is an
   *   RFC 3339 timestamp.
   * @returns {Promise<{ purgedCount: number }>} A {@link PurgeResult}.
   */
  purgeRuns(request) {
    if (!request || !request.olderThan) {
      throw new TypeError('purgeRuns requires an olderThan timestamp.');
    }
    const search = new URLSearchParams({ olderThan: request.olderThan });
    if (request.limit != null) search.set('limit', String(request.limit));
    return this._request('PURGE', `/runs${qs(search)}`, { signal: request.signal });
  }

  // ---- runners:read (the execution-host registry, §5.4) -----------------------------------------

  /**
   * `listRunners` — one page of the runner registry (§5.4): the execution hosts that have registered and heartbeat,
   * ordered by `runnerId`. Each runner is `{ runnerId, environment, address?, startedAt, lastSeenAt, maxConcurrency,
   * transports[], hostedVersions: [{ baseWorkflowId, versionNumber, hash, loaded }] }`; `environment` is the single
   * deployment environment the runner serves (§5.5); `lastSeenAt` is the most recent heartbeat (a
   * stale/dead runner is one whose heartbeat has lapsed). Read-only — runners self-register and heartbeat out of band.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ runners: object[], nextPageToken: (string|null) }>} A {@link RunnerPage}.
   */
  async listRunners(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const page = await this._request('GET', `/runners${qs(search)}`, { signal: query.signal });
    return { runners: page.runners ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `listRunners`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ runners: object[], nextPageToken: (string|null) }>}
   */
  async *listRunnersPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listRunners({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  // ---- runner authorizations (which runners may serve an environment, §5.5) ----------------------

  /**
   * `listEnvironmentRunnerAuthorizations` — one environment's runner roster: every runner that has registered for it,
   * with its authorization state, ordered by `runnerId`. Only a current administrator of the environment may list it
   * (`403`); an unknown/out-of-reach environment is `404`. Optionally filtered by `status` (`Pending`/`Authorized`/
   * `Revoked`); `limit`/`pageToken` page. Each item is an {@link EnvironmentRunnerAuthorizationView}
   * (`{ environment, runnerId, status, reason?, createdBy, createdAt, decidedBy?, decidedAt?, etag }`).
   * @param {string} name The environment.
   * @param {{ status?: string, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ authorizations: object[], nextPageToken: (string|null) }>} An {@link EnvironmentRunnerAuthorizationList}.
   */
  async listEnvironmentRunnerAuthorizations(name, query = {}) {
    if (!name) throw new TypeError('listEnvironmentRunnerAuthorizations requires a name.');
    const search = new URLSearchParams();
    if (query.status) search.set('status', query.status);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `${this._environmentPath(name)}/runners${qs(search)}`, { signal: query.signal });
    return { authorizations: result.authorizations ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listRunnerAuthorizations` — the approver inbox: runner authorizations across the environments the caller
   * administers, defaulting to `Pending` (the actionable to-do) when no `status` is given. With `environment`, that one
   * environment's queue (the caller must administer it, `403` otherwise). `limit`/`pageToken` page (keyset, status
   * filtered server-side).
   * @param {{ status?: string, environment?: string, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ authorizations: object[], nextPageToken: (string|null) }>} An {@link EnvironmentRunnerAuthorizationList}.
   */
  async listRunnerAuthorizations(query = {}) {
    const search = new URLSearchParams();
    if (query.status) search.set('status', query.status);
    if (query.environment) search.set('environment', query.environment);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/runnerAuthorizations${qs(search)}`, { signal: query.signal });
    return { authorizations: result.authorizations ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listRunnerAuthorizations`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ status?: string, environment?: string, limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ authorizations: object[], nextPageToken: (string|null) }>}
   */
  async *listRunnerAuthorizationsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listRunnerAuthorizations({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `authorizeRunner` — authorize a runner to serve an environment (idempotent; an already-authorized runner is returned
   * unchanged). The caller must be an administrator of the environment (`403`); the runner must have registered for it
   * (`404`); a concurrent decision is `409`.
   * @param {string} name The environment.
   * @param {string} runnerId The runner.
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link EnvironmentRunnerAuthorizationView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  authorizeRunner(name, runnerId, note = {}, opts = {}) {
    if (!name || !runnerId) throw new TypeError('authorizeRunner requires a name and runnerId.');
    return this._request('POST', `${this._runnerAuthorizationPath(name, runnerId)}`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `revokeRunner` — revoke a runner's authorization to serve an environment (unconditional and idempotent; an
   * already-revoked runner is returned unchanged). The caller must be an administrator of the environment (`403`); the
   * runner must have registered for it (`404`).
   * @param {string} name The environment.
   * @param {string} runnerId The runner.
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link EnvironmentRunnerAuthorizationView}. Throws {@link ProblemError} `403`/`404`.
   */
  revokeRunner(name, runnerId, note = {}, opts = {}) {
    if (!name || !runnerId) throw new TypeError('revokeRunner requires a name and runnerId.');
    return this._request('DELETE', `${this._runnerAuthorizationPath(name, runnerId)}`, { body: decisionNote(note), signal: opts.signal });
  }

  /** @private */
  _runnerAuthorizationPath(name, runnerId) {
    return `${this._environmentPath(name)}/runners/${encodeURIComponent(runnerId)}/authorization`;
  }

  // ---- catalog:read -----------------------------------------------------------------------------

  /**
   * `searchCatalog` — one page of catalog version summaries.
   * @param {{ q?: string, baseWorkflowId?: string, workflowIdPrefix?: string, tags?: string[], status?: string, owner?: string, distinctWorkflows?: boolean, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   *   `tags` are AND-matched; `q` is free-text over title/description; `owner` matches owner name/email;
   *   `workflowIdPrefix` is an anchored, case-insensitive prefix over the versioned workflow id (for type-ahead).
   *   `distinctWorkflows` collapses the result to one representative version per base workflow, keyset-paged by base id.
   * @returns {Promise<{ versions: object[], nextPageToken: (string|null) }>} A {@link CatalogPage}.
   */
  async searchCatalog(query = {}) {
    const search = new URLSearchParams();
    if (query.q) search.set('q', query.q);
    if (query.baseWorkflowId) search.set('baseWorkflowId', query.baseWorkflowId);
    if (query.workflowIdPrefix) search.set('workflowIdPrefix', query.workflowIdPrefix);
    for (const tag of query.tags ?? []) {
      if (tag) search.append('tag', tag);
    }
    if (query.status) search.set('status', query.status);
    if (query.owner) search.set('owner', query.owner);
    if (query.distinctWorkflows) search.set('distinctWorkflows', 'true');
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const page = await this._request('GET', `/catalog${qs(search)}`, { signal: query.signal });
    return { versions: page.versions ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `searchCatalog`, as an async iterator walking every page via the keyset `nextPageToken`.
   * @param {{ q?: string, baseWorkflowId?: string, tags?: string[], status?: string, owner?: string, limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ versions: object[], nextPageToken: (string|null) }>}
   */
  async *searchCatalogPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.searchCatalog({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `listCatalogVersions` — the versions of one base workflow id.
   * @param {string} baseWorkflowId
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ versions: object[], nextPageToken: (string|null) }>}
   */
  async listCatalogVersions(baseWorkflowId, query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const page = await this._request('GET', `/catalog/${encodeURIComponent(baseWorkflowId)}${qs(search)}`, { signal: query.signal });
    return { versions: page.versions ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `getCatalogVersion` — a version's metadata (no documents).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} A {@link CatalogVersionSummary}. Throws {@link ProblemError} `404` if absent.
   */
  getCatalogVersion(baseWorkflowId, versionNumber, opts = {}) {
    return this._request('GET', this._versionPath(baseWorkflowId, versionNumber), { signal: opts.signal });
  }

  /**
   * `listVersionAvailability` — the environments this workflow version has been made available in (§7.8 promotion),
   * ordered by environment.
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ availability: object[], nextPageToken: (string|null) }>} An {@link AvailabilityList}.
   */
  async listVersionAvailability(baseWorkflowId, versionNumber, query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `${this._versionPath(baseWorkflowId, versionNumber)}/availability${qs(search)}`, { signal: query.signal });
    return { availability: result.availability ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `makeVersionAvailable` — make this workflow version available in an environment (§7.8 promotion, idempotent). The
   * caller must administer the environment (`403`); readiness-gated — every source the version references must have a
   * usable credential in the environment (`409`). Returns the {@link AvailabilityEntry} (`201` created, `200` if it
   * was already available).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {string} environment
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The {@link AvailabilityEntry}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  makeVersionAvailable(baseWorkflowId, versionNumber, environment, opts = {}) {
    if (!environment) throw new TypeError('makeVersionAvailable requires an environment.');
    return this._request('PUT', `${this._versionPath(baseWorkflowId, versionNumber)}/availability/${encodeURIComponent(environment)}`, { signal: opts.signal });
  }

  /**
   * `deleteVersionAvailability` — withdraw this version's availability in an environment (§7.8). The caller must
   * administer the environment (`403`).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {string} environment
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>} Resolves on `204`. Throws {@link ProblemError} `403`/`404`.
   */
  async deleteVersionAvailability(baseWorkflowId, versionNumber, environment, opts = {}) {
    if (!environment) throw new TypeError('deleteVersionAvailability requires an environment.');
    await this._request('DELETE', `${this._versionPath(baseWorkflowId, versionNumber)}/availability/${encodeURIComponent(environment)}`, { signal: opts.signal });
  }

  /**
   * `getCatalogPackage` — the whole package archive (an opaque binary ZIP).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<Blob>} The package archive bytes.
   */
  getCatalogPackage(baseWorkflowId, versionNumber, opts = {}) {
    return this._request('GET', `${this._versionPath(baseWorkflowId, versionNumber)}/package`, { signal: opts.signal, raw: true });
  }

  /**
   * `getCatalogWorkflow` — just the version's Arazzo workflow document.
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The Arazzo workflow document.
   */
  getCatalogWorkflow(baseWorkflowId, versionNumber, opts = {}) {
    return this._request('GET', `${this._versionPath(baseWorkflowId, versionNumber)}/workflow`, { signal: opts.signal });
  }

  /**
   * `getCatalogWorkflowSchemas` — the version's precomputed schema metadata (typed inputs + each step's
   * resolved output types), for rendering strongly-typed forms without re-parsing the sources.
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The schema-metadata document. Throws {@link ProblemError} `404` if absent.
   */
  getCatalogWorkflowSchemas(baseWorkflowId, versionNumber, opts = {}) {
    return this._request('GET', `${this._versionPath(baseWorkflowId, versionNumber)}/schemas`, { signal: opts.signal });
  }

  /**
   * `getCatalogSource` — one named source document from a version's package.
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {string} sourceName
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The source document.
   */
  getCatalogSource(baseWorkflowId, versionNumber, sourceName, opts = {}) {
    return this._request('GET', `${this._versionPath(baseWorkflowId, versionNumber)}/sources/${encodeURIComponent(sourceName)}`, { signal: opts.signal });
  }

  /**
   * `validateCatalogValue` — validate a JSON value against the true JSON Schema of a target within a version
   * (a workflow's inputs, a step's request/response body, or a step's outputs object).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ kind: ('inputs'|'requestBody'|'responseBody'|'stepOutputs'), workflowId?: string, stepId?: string, status?: string }} target
   * @param {*} value The value to validate.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<{ valid: boolean, errors: Array<{ instancePath?: string, message: string, keyword?: string, schemaLocation?: string }> }>}
   */
  validateCatalogValue(baseWorkflowId, versionNumber, target, value, opts = {}) {
    return this._request('POST', `${this._versionPath(baseWorkflowId, versionNumber)}/validate`, { body: { target, value }, signal: opts.signal });
  }

  // ---- catalog:write ----------------------------------------------------------------------------

  /**
   * `addCatalogVersion` — upload a new immutable version as `multipart/form-data`.
   * @param {{ package: (Blob|ArrayBuffer|Uint8Array), owner: { name: string, email: string, team?: string, url?: string }, tags?: string[], securityTags?: Array<{ key: string, value: string }>, signal?: AbortSignal }} request
   *   `package` is the package archive (the `{workflow, sources}` content as the ZIP from `WorkflowPackage`).
   *   `securityTags` are non-internal reach labels (§14.2); the reserved `sys:` prefix is rejected (400).
   * @returns {Promise<object>} The added {@link CatalogVersionSummary}. Throws {@link ProblemError} `400`/`409`.
   */
  addCatalogVersion(request) {
    if (!request || request.package == null || !request.owner) {
      throw new TypeError('addCatalogVersion requires a package and an owner.');
    }
    const form = new FormData();
    const blob = request.package instanceof Blob ? request.package : new Blob([request.package], { type: 'application/octet-stream' });
    form.append('package', blob, 'package.awp');
    form.append('owner', new Blob([JSON.stringify(request.owner)], { type: 'application/json' }));
    for (const tag of request.tags ?? []) {
      if (tag) form.append('tags', tag);
    }
    // Complex parts are JSON parts (matching the server's MultipartFormDataSerializer / the `owner` part above), not
    // repeated fields. Omit entirely when empty so the server leaves the stamped internal tags untouched.
    if (request.securityTags?.length) {
      form.append('securityTags', new Blob([JSON.stringify(request.securityTags)], { type: 'application/json' }));
    }
    return this._request('POST', '/catalog', { form, signal: request.signal });
  }

  /**
   * `updateCatalogVersion` — update a version's governance metadata (owner / tags / status / securityTags).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ owner?: object, tags?: string[], status?: string, securityTags?: Array<{ key: string, value: string }> }} patch
   *   A present `securityTags` re-tags the non-internal labels (omit to leave unchanged); the server preserves the
   *   deployment-internal tags and rejects the reserved `sys:` prefix (400).
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link CatalogVersionSummary}.
   */
  updateCatalogVersion(baseWorkflowId, versionNumber, patch, opts = {}) {
    return this._request('PATCH', this._versionPath(baseWorkflowId, versionNumber), { body: patch ?? {}, signal: opts.signal });
  }

  /**
   * `obsoleteCatalogVersion` — mark a version `Obsolete` (a convenience over {@link #updateCatalogVersion}).
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link CatalogVersionSummary}.
   */
  obsoleteCatalogVersion(baseWorkflowId, versionNumber, opts = {}) {
    return this.updateCatalogVersion(baseWorkflowId, versionNumber, { status: 'Obsolete' }, opts);
  }

  // ---- catalog:purge ----------------------------------------------------------------------------

  /**
   * `deleteCatalogVersion` — delete a single version. Refused (`409`) while runs reference it.
   * @param {string} baseWorkflowId
   * @param {number} versionNumber
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>} Resolves on `204`.
   */
  async deleteCatalogVersion(baseWorkflowId, versionNumber, opts = {}) {
    await this._request('DELETE', this._versionPath(baseWorkflowId, versionNumber), { signal: opts.signal });
  }

  /**
   * `purgeCatalog` — bulk-reap obsolete versions with no referencing runs.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<{ purgedCount: number }>} A {@link PurgeResult}.
   */
  purgeCatalog(opts = {}) {
    return this._request('PURGE', '/catalog', { signal: opts.signal });
  }

  /** @private */
  _versionPath(baseWorkflowId, versionNumber) {
    return `/catalog/${encodeURIComponent(baseWorkflowId)}/versions/${encodeURIComponent(versionNumber)}`;
  }

  // ---- credentials:read -------------------------------------------------------------------------

  /**
   * `listCredentials` — one page of source credential bindings (references and non-secret metadata only — never
   * secret material), ordered by sourceName then environment. Each summary carries a derived `credentialStatus`
   * (`valid` | `expiringSoon` | `expired`). Page with `limit` and the opaque `pageToken` from a previous page's
   * `nextPageToken`.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ credentials: object[], nextPageToken: (string|null) }>} A {@link CredentialBindingList}.
   */
  async listCredentials(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/credentials${qs(search)}`, { signal: query.signal });
    return { credentials: result.credentials ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listCredentials`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ credentials: object[], nextPageToken: (string|null) }>}
   */
  async *listCredentialsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listCredentials({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `listEnvironments` — one page of deployment environments the caller's reach admits (§7.7), ordered by name. Page
   * with `limit` and the opaque `pageToken` from a previous page's `nextPageToken`.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ environments: object[], nextPageToken: (string|null) }>} An {@link EnvironmentList}.
   */
  async listEnvironments(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/environments${qs(search)}`, { signal: query.signal });
    return { environments: result.environments ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listEnvironments`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ environments: object[], nextPageToken: (string|null) }>}
   */
  async *listEnvironmentsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listEnvironments({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getEnvironment` — a single deployment environment by name (§7.7).
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} An {@link EnvironmentSummary}. Throws {@link ProblemError} `404` if absent.
   */
  getEnvironment(name, opts = {}) {
    if (!name) throw new TypeError('getEnvironment requires a name.');
    return this._request('GET', this._environmentPath(name), { signal: opts.signal });
  }

  /**
   * `createEnvironment` — create a governed, reach-scoped environment; the deployment grants the calling principal
   * administration of it (§7.7). Conflicts (`409`) if an environment with that name already exists in the caller's reach.
   * @param {{ name: string, displayName?: string, description?: string, managementTags?: Array<{key: string, value: string}>, signal?: AbortSignal }} body
   * @returns {Promise<object>} The created {@link EnvironmentSummary}. Throws {@link ProblemError} `400`/`409`.
   */
  createEnvironment(body) {
    if (!body || !body.name) throw new TypeError('createEnvironment requires a name.');
    const payload = { name: body.name };
    if (body.displayName) payload.displayName = body.displayName;
    if (body.description) payload.description = body.description;
    if (body.managementTags) payload.managementTags = body.managementTags;
    return this._request('POST', '/environments', { body: payload, signal: body.signal });
  }

  /**
   * `updateEnvironment` — replace an environment's mutable metadata (display name, description); the name, reach scope,
   * and created-* audit fields are immutable. The caller must be a current administrator (`403` otherwise).
   * @param {string} name
   * @param {{ displayName?: string, description?: string }} patch
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link EnvironmentSummary}. Throws {@link ProblemError} `400`/`403`/`404`/`409`.
   */
  updateEnvironment(name, patch, opts = {}) {
    if (!name) throw new TypeError('updateEnvironment requires a name.');
    return this._request('PUT', this._environmentPath(name), { body: patch ?? {}, signal: opts.signal });
  }

  /**
   * `deleteEnvironment` — delete an environment by name. The caller must be a current administrator (`403` otherwise).
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>} Resolves on `204`. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  async deleteEnvironment(name, opts = {}) {
    if (!name) throw new TypeError('deleteEnvironment requires a name.');
    await this._request('DELETE', this._environmentPath(name), { signal: opts.signal });
  }

  /**
   * `listEnvironmentAdministrators` — the administrator set of an environment (§7.7/§15). Each administrator is a
   * resolved identity ({@link listAdministrators}): a stable `digest` (the removal key), the deployment-mapped
   * `identity` grants it resolves from, and an optional resolved `kind`/`label` for display.
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<{ administrators: Array<{ digest: string, identity: Array<{ dimension: string, value: string }>, kind?: string, label?: string }> }>} An {@link AdministratorList}.
   */
  async listEnvironmentAdministrators(name, opts = {}) {
    if (!name) throw new TypeError('listEnvironmentAdministrators requires a name.');
    const result = await this._request('GET', this._environmentAdministratorsPath(name), { signal: opts.signal });
    return { administrators: result.administrators ?? [] };
  }

  /**
   * `addEnvironmentAdministrator` — add a resolved identity to an environment's administrator set (idempotent); the
   * caller must be a current administrator (`403`). Provide a resolved grantee from the picker (its `kind`, `value`,
   * and full `identity`) OR a single deployment-mapped `{ dimension, value }` grant. See {@link addAdministrator}.
   * @param {string} name
   * @param {{ value: string, dimension?: string, kind?: string, identity?: Array<{ dimension: string, value: string }>, label?: string, complete?: boolean }} member
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}. Throws {@link ProblemError} `400`/`403`/`404`/`409`.
   */
  addEnvironmentAdministrator(name, member, opts = {}) {
    if (!name) throw new TypeError('addEnvironmentAdministrator requires a name.');
    if (!member || !member.value || (!member.dimension && !(Array.isArray(member.identity) && member.identity.length > 0))) {
      throw new TypeError('addEnvironmentAdministrator requires a resolved grantee ({ kind, value, identity }) or a single { dimension, value } grant.');
    }
    return this._request('POST', `${this._environmentAdministratorsPath(name)}/members`, { body: member, signal: opts.signal });
  }

  /**
   * `removeEnvironmentAdministrator` — remove the administrator whose identity `digest` matches. The set may not be left
   * empty (`409`). The caller must be a current administrator (`403`).
   * @param {string} name
   * @param {string} digest
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}.
   */
  removeEnvironmentAdministrator(name, digest, opts = {}) {
    if (!name) throw new TypeError('removeEnvironmentAdministrator requires a name.');
    return this._request('DELETE', `${this._environmentAdministratorsPath(name)}/members/${encodeURIComponent(digest)}`, { signal: opts.signal });
  }

  /**
   * `transferEnvironmentAdministration` — replace the entire administrator set with the given identities (at least one);
   * an administrator may transfer administration away from itself. The caller must be a current administrator (`403`).
   * @param {string} name
   * @param {{ administrators: Array<{ dimension: string, value: string }> }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}. Throws {@link ProblemError} `400`/`403`/`404`/`409`.
   */
  transferEnvironmentAdministration(name, body, opts = {}) {
    if (!name) throw new TypeError('transferEnvironmentAdministration requires a name.');
    if (!body || !Array.isArray(body.administrators) || body.administrators.length === 0) {
      throw new TypeError('transferEnvironmentAdministration requires at least one administrator.');
    }
    return this._request('PUT', this._environmentAdministratorsPath(name), { body, signal: opts.signal });
  }

  /**
   * `listEnvironmentAvailability` — the workflow versions made available in this environment (§7.8), ordered by base
   * workflow id then version.
   * @param {string} name
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ availability: object[], nextPageToken: (string|null) }>} An {@link AvailabilityList}.
   */
  async listEnvironmentAvailability(name, query = {}) {
    if (!name) throw new TypeError('listEnvironmentAvailability requires a name.');
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `${this._environmentPath(name)}/availability${qs(search)}`, { signal: query.signal });
    return { availability: result.availability ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /** @private */
  _environmentPath(name) {
    return `/environments/${encodeURIComponent(name)}`;
  }

  /** @private */
  _environmentAdministratorsPath(name) {
    return `${this._environmentPath(name)}/administrators`;
  }

  /**
   * `listSources` — one page of registered sources (§7.6; the list omits each source's document, returned only on a
   * single read). Ordered by name. Page with `limit` and the opaque `pageToken`.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ sources: object[], nextPageToken: (string|null) }>} A {@link SourceList}.
   */
  async listSources(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/sources${qs(search)}`, { signal: query.signal });
    return { sources: result.sources ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listSources`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ sources: object[], nextPageToken: (string|null) }>}
   */
  async *listSourcesPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listSources({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getSource` — a single registered source WITH its OpenAPI/AsyncAPI `document` (§7.6).
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} A {@link Source}. Throws {@link ProblemError} `404` if not registered.
   */
  getSource(name, opts = {}) {
    return this._request('GET', `/sources/${encodeURIComponent(name)}`, { signal: opts.signal });
  }

  /**
   * `createSource` — register a new source (§7.6): its `name` (matching a workflow `sourceDescriptions` entry),
   * `type`, and `document`. Conflicts (`409`) if a source with that name already exists in the caller's reach.
   * @param {{ name: string, type: string, document: object, displayName?: string, description?: string, managementTags?: Array<{key: string, value: string}>, signal?: AbortSignal }} source
   * @returns {Promise<object>} The created {@link Source}.
   */
  createSource(source) {
    if (!source || !source.name || !source.type || source.document == null) {
      throw new TypeError('createSource requires a name, type, and document.');
    }
    const body = { name: source.name, type: source.type, document: source.document };
    if (source.displayName) body.displayName = source.displayName;
    if (source.description) body.description = source.description;
    if (source.managementTags) body.managementTags = source.managementTags;
    return this._request('POST', '/sources', { body, signal: source.signal });
  }

  /**
   * `getCredential` — one binding by its `(sourceName, environment)` key.
   * @param {string} sourceName
   * @param {string} environment
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} A {@link CredentialBindingSummary}. Throws {@link ProblemError} `404` if absent.
   */
  getCredential(sourceName, environment, opts = {}) {
    return this._request('GET', this._credentialPath(sourceName, environment), { signal: opts.signal });
  }

  // ---- credentials:write ------------------------------------------------------------------------

  /**
   * `createCredential` — create a binding. The body carries **references** (a `secretRef` of the form
   * `scheme://locator[#version]`) and non-secret metadata only; a value that is not a well-formed `secretRef`
   * is rejected (`400`), so secret material can never be persisted inline.
   * @param {{ sourceName: string, environment: string, authKind: string, secretRefs: Array<{name: string, ref: string}>, config?: Array<{key: string, value: string}>, managementTags?: Array<{key: string, value: string}>, usageGrants?: Array<{dimension: string, value: string}>, expiresAt?: string, description?: string }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The created {@link CredentialBindingSummary}. Throws {@link ProblemError} `400`/`409`.
   */
  createCredential(body, opts = {}) {
    if (!body || !body.sourceName || !body.environment) {
      throw new TypeError('createCredential requires a sourceName and environment.');
    }
    return this._request('POST', '/credentials', { body, signal: opts.signal });
  }

  /**
   * `updateCredential` — replace a binding's references and non-secret metadata (the `(sourceName, environment)`
   * identity and created-* audit fields are immutable). Re-pointing a `secretRef` is how a credential is
   * rotated. A value that is not a well-formed `secretRef` is rejected (`400`).
   * @param {string} sourceName
   * @param {string} environment
   * @param {object} body The replacement reference set + metadata.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link CredentialBindingSummary}. Throws {@link ProblemError} `400`/`404`.
   */
  updateCredential(sourceName, environment, body, opts = {}) {
    return this._request('PUT', this._credentialPath(sourceName, environment), { body: body ?? {}, signal: opts.signal });
  }

  /**
   * `deleteCredential` — delete a binding by its `(sourceName, environment)` key.
   * @param {string} sourceName
   * @param {string} environment
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>} Resolves on `204`. Throws {@link ProblemError} `404` if absent.
   */
  async deleteCredential(sourceName, environment, opts = {}) {
    await this._request('DELETE', this._credentialPath(sourceName, environment), { signal: opts.signal });
  }

  /** @private */
  _credentialPath(sourceName, environment) {
    return `/credentials/${encodeURIComponent(sourceName)}/${encodeURIComponent(environment)}`;
  }

  // ---- administrators:read ----------------------------------------------------------------------

  /**
   * `listAdministrators` — the administrator set of a base workflow id (§15). Each administrator is a resolved
   * identity: a stable `digest` (the removal key), the `identity` as the deployment-mapped `{ dimension, value }`
   * grants it resolves from (a multi-tag grantee yields several), and the optional resolved `kind`/`label` for
   * display. An unknown base id (or one with no established administration) is an empty set, not an error.
   * @param {string} baseWorkflowId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<{ administrators: Array<{ digest: string, identity: Array<{ dimension: string, value: string }>, kind?: string, label?: string }> }>} An {@link AdministratorList}.
   */
  async listAdministrators(baseWorkflowId, opts = {}) {
    const result = await this._request('GET', this._administratorsPath(baseWorkflowId), { signal: opts.signal });
    return { administrators: result.administrators ?? [] };
  }

  // ---- administrators:write ---------------------------------------------------------------------

  /**
   * `addAdministrator` — add a resolved identity to the base id's administrator set (idempotent). The caller must
   * be a current administrator (`403` otherwise). Provide EITHER a resolved grantee from the picker (its `kind`,
   * searchable `value`, and full `identity` — the `{ dimension, value }` grants of {@link searchGrantees} — which
   * names a multi-tag grantee exactly) OR, for the simple case, a single deployment-mapped `{ dimension, value }`
   * grant (the kind is inferred from the dimension). `value` is required in both forms.
   * @param {{ value: string, dimension?: string, kind?: string, identity?: Array<{ dimension: string, value: string }>, label?: string, complete?: boolean }} member
   * @param {string} baseWorkflowId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}. Throws {@link ProblemError} `400`/`403`/`409`.
   */
  addAdministrator(baseWorkflowId, member, opts = {}) {
    if (!member || !member.value || (!member.dimension && !(Array.isArray(member.identity) && member.identity.length > 0))) {
      throw new TypeError('addAdministrator requires a resolved grantee ({ kind, value, identity }) or a single { dimension, value } grant.');
    }
    return this._request('POST', `${this._administratorsPath(baseWorkflowId)}/members`, { body: member, signal: opts.signal });
  }

  /**
   * `removeAdministrator` — remove the administrator whose identity `digest` matches (the key from
   * {@link listAdministrators}). The set may not be left empty — removing the last administrator is refused
   * (`409`). The caller must be a current administrator (`403` otherwise).
   * @param {string} baseWorkflowId
   * @param {string} digest
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}.
   */
  removeAdministrator(baseWorkflowId, digest, opts = {}) {
    return this._request('DELETE', `${this._administratorsPath(baseWorkflowId)}/members/${encodeURIComponent(digest)}`, { signal: opts.signal });
  }

  /**
   * `transferAdministration` — replace the entire administrator set with the given identities (at least one);
   * an administrator may transfer administration away from itself. The caller must be a current administrator
   * (`403` otherwise).
   * @param {string} baseWorkflowId
   * @param {{ administrators: Array<{ dimension: string, value: string }> }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The resulting {@link AdministratorList}. Throws {@link ProblemError} `400`/`403`/`409`.
   */
  transferAdministration(baseWorkflowId, body, opts = {}) {
    if (!body || !Array.isArray(body.administrators) || body.administrators.length === 0) {
      throw new TypeError('transferAdministration requires at least one administrator.');
    }
    return this._request('PUT', this._administratorsPath(baseWorkflowId), { body, signal: opts.signal });
  }

  /** @private */
  _administratorsPath(baseWorkflowId) {
    return `/administrators/${encodeURIComponent(baseWorkflowId)}`;
  }

  // ---- security rules (§14.2) — the reusable reach vocabulary -----------------------------------

  /**
   * `searchSecurityRules` — a keyset page of the deployment's row-security rules (`GET /security/rules`): the reusable
   * scope vocabulary a grant binds read/write/purge with, ordered by name. `q` filters by a case-insensitive substring
   * of the name or expression; `limit`/`pageToken` page. Requires `security:read`.
   * @param {{ q?: string, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ rules: object[], nextPageToken: (string|null) }>} A {@link SecurityRuleList} — each rule is
   *   `{ name, expression, description?, createdBy, createdAt, lastUpdatedBy?, lastUpdatedAt?, etag }`.
   */
  async searchSecurityRules(query = {}) {
    const search = new URLSearchParams();
    if (query.q) search.set('q', query.q);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/security/rules${qs(search)}`, { signal: query.signal });
    return { rules: result.rules ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `searchSecurityRules`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ q?: string, limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ rules: object[], nextPageToken: (string|null) }>}
   */
  async *searchSecurityRulesPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.searchSecurityRules({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `listSecurityOrderings` — the deployment's configured ordered tag dimensions (`GET /security/orderings`): each is
   * `{ dimension, labels }` with the labels in ascending order, the ranking the ordered rule comparisons
   * (`<`/`<=`/`>`/`>=`) use. An authoring UI reads these to offer the ordered/classification templates with the exact
   * labels the policy enforces. Requires `security:read`.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<{ orderings: Array<{ dimension: string, labels: string[] }> }>} A {@link SecurityOrderingList}.
   */
  async listSecurityOrderings(opts = {}) {
    const result = await this._request('GET', '/security/orderings', { signal: opts.signal });
    return { orderings: result.orderings ?? [] };
  }

  /**
   * `searchSecurityBindings` — a keyset page of the deployment's claim→rule bindings (`GET /security/bindings`): each maps
   * a principal claim to per-verb (read/write/purge) reach, ordered by `(order, id)`. `q` filters by a case-insensitive
   * substring of the claim type, claim value, or description; `limit`/`pageToken` page. Requires `security:read`.
   * @param {{ q?: string, limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ bindings: object[], nextPageToken: (string|null) }>} A {@link SecurityBindingList} — each binding
   *   is `{ id, claimType, claimValue?, read, write, purge, order, description?, createdBy, createdAt, etag }` where a verb
   *   grant is `{ unrestricted?: boolean, ruleNames?: string[] }`.
   */
  async searchSecurityBindings(query = {}) {
    const search = new URLSearchParams();
    if (query.q) search.set('q', query.q);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/security/bindings${qs(search)}`, { signal: query.signal });
    return { bindings: result.bindings ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `searchSecurityBindings`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ q?: string, limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ bindings: object[], nextPageToken: (string|null) }>}
   */
  async *searchSecurityBindingsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.searchSecurityBindings({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getSecurityBinding` — fetch a single binding by id (`GET /security/bindings/{bindingId}`). Requires `security:read`.
   * @param {string} bindingId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The {@link SecurityBindingSummary}. Throws {@link ProblemError} `404`.
   */
  getSecurityBinding(bindingId, opts = {}) {
    if (!bindingId) throw new TypeError('getSecurityBinding requires a binding id.');
    return this._request('GET', `/security/bindings/${encodeURIComponent(bindingId)}`, { signal: opts.signal });
  }

  // ---- security:write ---------------------------------------------------------------------------

  /**
   * `createSecurityBinding` — create a claim→rule binding (`POST /security/bindings`). The server **rejects
   * self-elevation** (`403`): a caller may not grant a claim it holds write/purge reach — that goes through the
   * access-request flow. Requires `security:write`.
   * @param {{ claimType: string, claimValue?: string, read?: object, write?: object, purge?: object, order?: number, description?: string }} body
   *   Each verb grant is `{ unrestricted: boolean }` or `{ ruleNames: string[] }`; omit a verb to deny it.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The created {@link SecurityBindingSummary}.
   */
  createSecurityBinding(body, opts = {}) {
    if (!body || !body.claimType) throw new TypeError('createSecurityBinding requires a claimType.');
    return this._request('POST', '/security/bindings', { body, signal: opts.signal });
  }

  /**
   * `updateSecurityBinding` — replace a binding's content (`PUT /security/bindings/{bindingId}`). Self-elevation is
   * rejected (`403`); `404` if absent. Requires `security:write`.
   * @param {string} bindingId
   * @param {{ claimType: string, claimValue?: string, read?: object, write?: object, purge?: object, order?: number, description?: string }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link SecurityBindingSummary}.
   */
  updateSecurityBinding(bindingId, body, opts = {}) {
    if (!bindingId) throw new TypeError('updateSecurityBinding requires a binding id.');
    if (!body || !body.claimType) throw new TypeError('updateSecurityBinding requires a claimType.');
    return this._request('PUT', `/security/bindings/${encodeURIComponent(bindingId)}`, { body, signal: opts.signal });
  }

  /**
   * `deleteSecurityBinding` — delete a binding by id (`DELETE /security/bindings/{bindingId}`, `404` if absent).
   * Requires `security:write`.
   * @param {string} bindingId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>}
   */
  async deleteSecurityBinding(bindingId, opts = {}) {
    if (!bindingId) throw new TypeError('deleteSecurityBinding requires a binding id.');
    await this._request('DELETE', `/security/bindings/${encodeURIComponent(bindingId)}`, { signal: opts.signal });
  }

  /**
   * `getSecurityRule` — fetch a single rule by name (`GET /security/rules/{ruleName}`). Requires `security:read`.
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The {@link SecurityRuleSummary}. Throws {@link ProblemError} `404`.
   */
  getSecurityRule(name, opts = {}) {
    if (!name) throw new TypeError('getSecurityRule requires a rule name.');
    return this._request('GET', `/security/rules/${encodeURIComponent(name)}`, { signal: opts.signal });
  }

  // ---- security:write ---------------------------------------------------------------------------

  /**
   * `createSecurityRule` — create a named rule (`POST /security/rules`); the server validates `expression`
   * against the security-rule grammar (`400` on a malformed expression, `409` on a duplicate name). Requires
   * `security:write`.
   * @param {{ name: string, expression: string, description?: string }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The created {@link SecurityRuleSummary}.
   */
  createSecurityRule(body, opts = {}) {
    if (!body || !body.name || !body.expression) {
      throw new TypeError('createSecurityRule requires a name and an expression.');
    }
    const payload = { name: body.name, expression: body.expression };
    if (body.description) payload.description = body.description;
    return this._request('POST', '/security/rules', { body: payload, signal: opts.signal });
  }

  /**
   * `updateSecurityRule` — replace a rule's content (`PUT /security/rules/{ruleName}`); the server validates the
   * `expression` (`400` on malformed, `404` if absent). Requires `security:write`.
   * @param {string} name
   * @param {{ expression: string, description?: string }} body
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link SecurityRuleSummary}.
   */
  updateSecurityRule(name, body, opts = {}) {
    if (!name) throw new TypeError('updateSecurityRule requires a rule name.');
    if (!body || !body.expression) throw new TypeError('updateSecurityRule requires an expression.');
    const payload = { expression: body.expression };
    if (body.description) payload.description = body.description;
    return this._request('PUT', `/security/rules/${encodeURIComponent(name)}`, { body: payload, signal: opts.signal });
  }

  /**
   * `deleteSecurityRule` — delete a rule by name (`DELETE /security/rules/{ruleName}`, `404` if absent). Requires
   * `security:write`.
   * @param {string} name
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>}
   */
  async deleteSecurityRule(name, opts = {}) {
    if (!name) throw new TypeError('deleteSecurityRule requires a rule name.');
    await this._request('DELETE', `/security/rules/${encodeURIComponent(name)}`, { signal: opts.signal });
  }

  // ---- identity / grantee resolution (§16.5.4) --------------------------------------------------

  /**
   * `searchGrantees` — resolve real `person`/`team`/`role`/`workflow` grantees to their exact `sys:` identity
   * for a grant (`GET /identity/grantees`), via the deployment's directory/IdP search and the store-indexed
   * typeahead over identities Arazzo has already observed. Reach-filtered and `complete`-reported server-side, so
   * a picker can name a real subject instead of hand-assembling a `{dimension, value}` tuple. Requires
   * `administrators:read`.
   * @param {{ q?: string, kind?: string, source?: ('observed'|'directory'), limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ grantees: object[], nextPageToken: (string|null) }>} A {@link GranteeList} — each grantee is
   *   `{ kind, value, label?, identity: Array<{dimension,value}>, source, complete }`.
   */
  async searchGrantees(query = {}) {
    const search = new URLSearchParams();
    if (query.q) search.set('q', query.q);
    if (query.kind) search.set('kind', query.kind);
    if (query.source) search.set('source', query.source);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const page = await this._request('GET', `/identity/grantees${qs(search)}`, { signal: query.signal });
    return { grantees: page.grantees ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  // ---- access requests (§16.5) ------------------------------------------------------------------

  /**
   * `submitAccessRequest` — request elevated, time-bound access to a workflow. The requesting subject is taken
   * from the caller (a request can never target a third party). If the caller is eligible to self-elevate exactly
   * this, the request is auto-approved; otherwise it is created `Pending` an administrator's decision.
   * @param {{ baseWorkflowId: string, requestedScopes: string[], reason?: string, requestedDurationSeconds?: number, signal?: AbortSignal }} request
   *   `requestedScopes` must hold at least one scope; an approval grants at most these, capped to run access.
   * @returns {Promise<object>} The created {@link AccessRequestView}. Throws {@link ProblemError} `400`.
   */
  submitAccessRequest(request) {
    if (!request || !request.baseWorkflowId || !Array.isArray(request.requestedScopes) || request.requestedScopes.length === 0) {
      throw new TypeError('submitAccessRequest requires a baseWorkflowId and at least one requestedScope.');
    }
    const body = { baseWorkflowId: request.baseWorkflowId, requestedScopes: request.requestedScopes };
    if (request.reason) body.reason = request.reason;
    if (request.requestedDurationSeconds != null) body.requestedDurationSeconds = request.requestedDurationSeconds;
    return this._request('POST', '/accessRequests', { body, signal: request.signal });
  }

  /**
   * `listAccessRequests` — a keyset page (oldest first by `(createdAt, id)`). With `baseWorkflowId`, that workflow's
   * request queue (the caller must be an administrator of it, `403` otherwise). Without `baseWorkflowId`, `scope` selects
   * the view: `'mine'` (default) the caller's own requests; `'queue'` the approver inbox — every request across the
   * workflows the caller administers. Optionally filtered by `status`; `limit`/`pageToken` page.
   * @param {{ status?: string, baseWorkflowId?: string, scope?: ('mine'|'queue'), limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ accessRequests: object[], nextPageToken: (string|null) }>} An {@link AccessRequestList}, oldest first.
   */
  async listAccessRequests(query = {}) {
    const search = new URLSearchParams();
    if (query.status) search.set('status', query.status);
    if (query.baseWorkflowId) search.set('baseWorkflowId', query.baseWorkflowId);
    if (query.scope) search.set('scope', query.scope);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/accessRequests${qs(search)}`, { signal: query.signal });
    return { accessRequests: result.accessRequests ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listAccessRequests`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ status?: string, baseWorkflowId?: string, scope?: ('mine'|'queue'), limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ accessRequests: object[], nextPageToken: (string|null) }>}
   */
  async *listAccessRequestsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listAccessRequests({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getAccessRequest` — a single request. The caller must be its requester or an administrator of its workflow.
   * @param {string} requestId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} An {@link AccessRequestView}. Throws {@link ProblemError} `403`/`404`.
   */
  getAccessRequest(requestId, opts = {}) {
    return this._request('GET', this._accessRequestPath(requestId), { signal: opts.signal });
  }

  /**
   * `approveAccessRequest` — approve a pending request, writing the capped, time-boxed grant (run access only,
   * scoped to the workflow). The caller must be an administrator of the target workflow.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AccessRequestView}. Throws {@link ProblemError} `400`/`403`/`404`/`409`.
   */
  approveAccessRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._accessRequestPath(requestId)}/approve`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `approveAccessRequestAsEligible` — approve a pending request as durable eligibility (§16.5.3): the requester
   * may self-elevate it JIT rather than receiving a live grant now. The caller must be an administrator.
   * @param {string} requestId
   * @param {{ reason?: string, eligibilityWindowSeconds?: number }} [note] `eligibilityWindowSeconds` bounds how
   *   long the eligibility lasts; absent means standing eligibility.
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AccessRequestView}. Throws {@link ProblemError} `400`/`403`/`404`/`409`.
   */
  approveAccessRequestAsEligible(requestId, note = {}, opts = {}) {
    const body = decisionNote(note);
    if (note && note.eligibilityWindowSeconds != null) body.eligibilityWindowSeconds = note.eligibilityWindowSeconds;
    return this._request('POST', `${this._accessRequestPath(requestId)}/approveAsEligible`, { body, signal: opts.signal });
  }

  /**
   * `denyAccessRequest` — deny a pending request. The caller must be an administrator of the target workflow.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AccessRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  denyAccessRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._accessRequestPath(requestId)}/deny`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `withdrawAccessRequest` — withdraw a pending request. Only the requester (its own subject) may withdraw it.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AccessRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  withdrawAccessRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._accessRequestPath(requestId)}/withdraw`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `revokeAccessRequest` — revoke an approved grant early: the entitlement is deleted (access stops at the next
   * resolution, fail-safe) and the request is marked revoked. The caller must be an administrator.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AccessRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  revokeAccessRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._accessRequestPath(requestId)}/revoke`, { body: decisionNote(note), signal: opts.signal });
  }

  /** @private */
  _accessRequestPath(requestId) {
    return `/accessRequests/${encodeURIComponent(requestId)}`;
  }

  // ---- availability requests ("promotion" requests, §7.8) ---------------------------------------

  /**
   * `submitAvailabilityRequest` — request that a workflow version be made available in an environment. The requesting
   * identity is taken from the caller; the request is created `Pending` an environment administrator's decision (an
   * approval makes the version available, subject to the §7.7 readiness gate).
   * @param {{ baseWorkflowId: string, versionNumber: number, environment: string, reason?: string, signal?: AbortSignal }} request
   * @returns {Promise<object>} The created {@link AvailabilityRequestView}. Throws {@link ProblemError} `400`.
   */
  submitAvailabilityRequest(request) {
    if (!request || !request.baseWorkflowId || request.versionNumber == null || !request.environment) {
      throw new TypeError('submitAvailabilityRequest requires a baseWorkflowId, versionNumber, and environment.');
    }
    const body = { baseWorkflowId: request.baseWorkflowId, versionNumber: request.versionNumber, environment: request.environment };
    if (request.reason) body.reason = request.reason;
    return this._request('POST', '/availabilityRequests', { body, signal: request.signal });
  }

  /**
   * `listAvailabilityRequests` — a keyset page (oldest first by `(createdAt, id)`). With `environment`, that
   * environment's request queue (the caller must be an administrator of it, `403` otherwise). Without `environment`,
   * `scope` selects the view: `'mine'` (default) the caller's own requests; `'queue'` the approver inbox — every request
   * across the environments the caller administers. Optionally filtered by `status`; `limit`/`pageToken` page.
   * @param {{ status?: string, environment?: string, scope?: ('mine'|'queue'), limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ availabilityRequests: object[], nextPageToken: (string|null) }>} An {@link AvailabilityRequestList}, oldest first.
   */
  async listAvailabilityRequests(query = {}) {
    const search = new URLSearchParams();
    if (query.status) search.set('status', query.status);
    if (query.environment) search.set('environment', query.environment);
    if (query.scope) search.set('scope', query.scope);
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const result = await this._request('GET', `/availabilityRequests${qs(search)}`, { signal: query.signal });
    return { availabilityRequests: result.availabilityRequests ?? [], nextPageToken: result.nextPageToken ?? null };
  }

  /**
   * `listAvailabilityRequests`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ status?: string, environment?: string, scope?: ('mine'|'queue'), limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ availabilityRequests: object[], nextPageToken: (string|null) }>}
   */
  async *listAvailabilityRequestsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listAvailabilityRequests({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getAvailabilityRequest` — a single request. The caller must be its requester or an administrator of its environment.
   * @param {string} requestId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} An {@link AvailabilityRequestView}. Throws {@link ProblemError} `403`/`404`.
   */
  getAvailabilityRequest(requestId, opts = {}) {
    return this._request('GET', this._availabilityRequestPath(requestId), { signal: opts.signal });
  }

  /**
   * `approveAvailabilityRequest` — approve a pending request, making the workflow version available in the target
   * environment (readiness-gated, §7.7). The caller must be an administrator of that environment.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AvailabilityRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  approveAvailabilityRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._availabilityRequestPath(requestId)}/approve`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `denyAvailabilityRequest` — deny a pending request. The caller must be an administrator of the target environment.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AvailabilityRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  denyAvailabilityRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._availabilityRequestPath(requestId)}/deny`, { body: decisionNote(note), signal: opts.signal });
  }

  /**
   * `withdrawAvailabilityRequest` — withdraw a pending request. Only the requester may withdraw it.
   * @param {string} requestId
   * @param {{ reason?: string }} [note]
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} The updated {@link AvailabilityRequestView}. Throws {@link ProblemError} `403`/`404`/`409`.
   */
  withdrawAvailabilityRequest(requestId, note = {}, opts = {}) {
    return this._request('POST', `${this._availabilityRequestPath(requestId)}/withdraw`, { body: decisionNote(note), signal: opts.signal });
  }

  /** @private */
  _availabilityRequestPath(requestId) {
    return `/availabilityRequests/${encodeURIComponent(requestId)}`;
  }

  // ---- internals --------------------------------------------------------------------------------

  /**
   * @private
   * @param {string} method
   * @param {string} path
   * @param {{ body?: any, form?: FormData, raw?: boolean, signal?: AbortSignal }} [opts]
   */
  async _request(method, path, opts = {}) {
    const url = `${this._baseUrl}${path}`;
    /** @type {RequestInit} */
    const init = { method, headers: { Accept: opts.raw ? 'application/octet-stream' : 'application/json' } };

    if (opts.form !== undefined) {
      // multipart/form-data: let fetch set the Content-Type (with its boundary).
      init.body = opts.form;
    } else if (opts.body !== undefined) {
      init.headers['Content-Type'] = 'application/json';
      init.body = JSON.stringify(opts.body);
    }
    init.signal = opts.signal ?? this._signal;

    let response;
    if (this._fetch) {
      response = await this._fetch(url, init);
    } else {
      if (this._getAuthHeader) {
        const header = await this._getAuthHeader();
        if (header) init.headers.Authorization = header;
      } else {
        init.credentials = this._credentials;
      }
      response = await fetch(url, init);
    }

    if (!response.ok) {
      throw await toProblemError(response);
    }

    if (response.status === 204) {
      return undefined;
    }

    if (opts.raw) {
      return response.blob();
    }

    const text = await response.text();
    return text ? JSON.parse(text) : undefined;
  }
}

/** @param {URLSearchParams} search */
function qs(search) {
  const s = search.toString();
  return s ? `?${s}` : '';
}

/** Normalise a `Date` or date-ish string to an RFC 3339 / ISO 8601 instant for a query parameter. */
function toInstant(value) {
  return value instanceof Date ? value.toISOString() : String(value);
}

/** Build the optional `AccessRequestDecisionNote` body — `{ reason }` when supplied, else an empty object. */
function decisionNote(note) {
  const body = {};
  if (note && note.reason) body.reason = note.reason;
  return body;
}

/**
 * Builds a {@link ProblemError} from a non-2xx response, tolerating a missing/non-JSON body.
 * @param {Response} response
 * @returns {Promise<ProblemError>}
 */
async function toProblemError(response) {
  let problem = {};
  try {
    const text = await response.text();
    if (text) {
      const parsed = JSON.parse(text);
      if (parsed && typeof parsed === 'object') problem = parsed;
    }
  } catch {
    // Non-JSON or empty body — fall back to the status line.
  }
  if (problem.status == null) problem.status = response.status;
  return new ProblemError(response.status, problem, response);
}
