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

  // ---- internals --------------------------------------------------------------------------------

  /**
   * @private
   * @param {string} method
   * @param {string} path
   * @param {{ body?: any, signal?: AbortSignal }} [opts]
   */
  async _request(method, path, opts = {}) {
    const url = `${this._baseUrl}${path}`;
    /** @type {RequestInit} */
    const init = { method, headers: { Accept: 'application/json' } };

    if (opts.body !== undefined) {
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
