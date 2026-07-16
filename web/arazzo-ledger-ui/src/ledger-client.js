// Ledger service — API client (no DOM).
//
// A dependency-free ES module over the ledger service's read API: GET /reconciliations (paged),
// GET /reconciliations/{runId}, and GET /accounts (paged). Like the onboarding console this is a SEPARATE
// application from the Arazzo workflow engine — the reconciliation product ships independently and merely consumes
// the engine — so it carries its own small client.
//
//   import { LedgerClient } from './ledger-client.js';
//   const client = new LedgerClient();                       // same-origin (the ledger host serves this app)
//   const { reconciliations, nextPageToken } = await client.listReconciliations({ limit: 50 });

/**
 * An error carrying an RFC 9457 `application/problem+json` (or any JSON error) body from the ledger service.
 */
export class ProblemError extends Error {
  /**
   * @param {number} status HTTP status code.
   * @param {object} [body] The parsed error body, when available.
   * @param {Response} [response] The originating response.
   */
  constructor(status, body = undefined, response = undefined) {
    super((body && (body.title || body.detail)) || `Ledger request failed (${status}).`);
    this.name = 'ProblemError';
    this.status = status;
    /** @type {object|undefined} */ this.body = body;
    /** @type {Response|undefined} */ this.response = response;
  }
}

/**
 * A thin client over the ledger service. Construct one and share it.
 */
export class LedgerClient {
  /**
   * @param {{ baseUrl?: string, fetch?: typeof fetch, credentials?: RequestCredentials, signal?: AbortSignal }} [options]
   *   `baseUrl` defaults to '' (same origin — the ledger host serves this app alongside its API).
   */
  constructor(options = {}) {
    /** @private */ this._baseUrl = String(options.baseUrl ?? '').replace(/\/+$/, '');
    /** @private */ this._fetch = options.fetch;
    /** @private */ this._credentials = options.credentials ?? 'same-origin';
    /** @private */ this._signal = options.signal;
  }

  /**
   * `listReconciliations` — one page of reconciliation runs, newest first.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ reconciliations: object[], nextPageToken: (string|null) }>}
   */
  async listReconciliations(query = {}) {
    const page = await this._request('GET', `/reconciliations${pageQuery(query)}`, query.signal);
    return { reconciliations: page.reconciliations ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `listReconciliations`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ reconciliations: object[], nextPageToken: (string|null) }>}
   */
  async *listReconciliationsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listReconciliations({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getReconciliation` — a single run's full view.
   * @param {string} runId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} Throws {@link ProblemError} with `status === 404` if unknown.
   */
  getReconciliation(runId, opts = {}) {
    return this._request('GET', `/reconciliations/${encodeURIComponent(runId)}`, opts.signal);
  }

  /**
   * `listAccounts` — one page of the ledger account book.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ accounts: object[], nextPageToken: (string|null) }>}
   */
  async listAccounts(query = {}) {
    const page = await this._request('GET', `/accounts${pageQuery(query)}`, query.signal);
    return { accounts: page.accounts ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /** @private */
  async _request(method, path, signal) {
    const fetchImpl = this._fetch ?? globalThis.fetch;
    const response = await fetchImpl(`${this._baseUrl}${path}`, {
      method,
      headers: { accept: 'application/json' },
      credentials: this._credentials,
      signal: signal ?? this._signal,
    });
    if (!response.ok) {
      let body;
      try {
        body = await response.json();
      } catch {
        body = undefined;
      }

      throw new ProblemError(response.status, body, response);
    }

    if (response.status === 204) {
      return undefined;
    }

    return response.json();
  }
}

function pageQuery(query) {
  const search = new URLSearchParams();
  if (query.limit != null) search.set('limit', String(query.limit));
  if (query.pageToken) search.set('pageToken', query.pageToken);
  const suffix = search.toString();
  return suffix ? `?${suffix}` : '';
}
