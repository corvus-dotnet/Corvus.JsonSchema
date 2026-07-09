// Onboarding service — API client (no DOM).
//
// A dependency-free ES module over the onboarding service's read API: GET /accounts (paged) and
// GET /accounts/{accountId}. This is a SEPARATE application from the Arazzo control plane — the onboarding
// product ships independently and merely consumes the workflow engine — so it has its own small client, not the
// control-plane one.
//
//   import { OnboardingClient } from './onboarding-client.js';
//   const client = new OnboardingClient();                 // same-origin (the onboarding host serves this app)
//   const { accounts, nextPageToken } = await client.listAccounts({ limit: 50 });

/**
 * An error carrying an RFC 9457 `application/problem+json` (or any JSON error) body from the onboarding service.
 */
export class ProblemError extends Error {
  /**
   * @param {number} status HTTP status code.
   * @param {object} [body] The parsed error body, when available.
   * @param {Response} [response] The originating response.
   */
  constructor(status, body = undefined, response = undefined) {
    super((body && (body.title || body.detail)) || `Onboarding request failed (${status}).`);
    this.name = 'ProblemError';
    this.status = status;
    /** @type {object|undefined} */ this.body = body;
    /** @type {Response|undefined} */ this.response = response;
  }
}

/**
 * A thin client over the onboarding service. Construct one and share it.
 */
export class OnboardingClient {
  /**
   * @param {{ baseUrl?: string, fetch?: typeof fetch, credentials?: RequestCredentials, signal?: AbortSignal }} [options]
   *   `baseUrl` defaults to '' (same origin — the onboarding host serves this app alongside its API).
   */
  constructor(options = {}) {
    /** @private */ this._baseUrl = String(options.baseUrl ?? '').replace(/\/+$/, '');
    /** @private */ this._fetch = options.fetch;
    /** @private */ this._credentials = options.credentials ?? 'same-origin';
    /** @private */ this._signal = options.signal;
  }

  /**
   * `listAccounts` — one page of onboarding accounts, newest first.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ accounts: object[], nextPageToken: (string|null) }>}
   */
  async listAccounts(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const suffix = search.toString();
    const page = await this._request('GET', `/accounts${suffix ? `?${suffix}` : ''}`, query.signal);
    return { accounts: page.accounts ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `listAccounts`, as an async iterator that walks every page via the keyset `nextPageToken`.
   * @param {{ limit?: number, signal?: AbortSignal }} [query]
   * @returns {AsyncGenerator<{ accounts: object[], nextPageToken: (string|null) }>}
   */
  async *listAccountsPaged(query = {}) {
    let pageToken;
    do {
      const page = await this.listAccounts({ ...query, pageToken });
      yield page;
      pageToken = page.nextPageToken || undefined;
    } while (pageToken);
  }

  /**
   * `getAccount` — a single account's full AccountView aggregate.
   * @param {string} accountId
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<object>} Throws {@link ProblemError} with `status === 404` if unknown.
   */
  getAccount(accountId, opts = {}) {
    return this._request('GET', `/accounts/${encodeURIComponent(accountId)}`, opts.signal);
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
