// KYC service — API client (no DOM).
//
// A dependency-free ES module over the KYC service's API: GET /verifications (paged), GET /accounts/{id}/verification,
// and POST /accounts/{id}/kyc-verdict (the manual-recovery action). This is a SEPARATE application from the Arazzo
// control plane — the KYC product ships independently and merely consumes the workflow engine — so it has its own
// small client, not the control-plane one.
//
//   import { KycClient } from './kyc-client.js';
//   const client = new KycClient();                        // same-origin (the KYC host serves this app)
//   const { verifications, nextPageToken } = await client.listVerifications({ limit: 50 });
//   await client.submitVerdict(accountId, { verified: true, score: 0.9, fullName: 'Ada Lovelace' });

/**
 * An error carrying an RFC 9457 `application/problem+json` (or any JSON error) body from the KYC service.
 */
export class ProblemError extends Error {
  /**
   * @param {number} status HTTP status code.
   * @param {object} [body] The parsed error body, when available.
   * @param {Response} [response] The originating response.
   */
  constructor(status, body = undefined, response = undefined) {
    super((body && (body.title || body.detail)) || `KYC request failed (${status}).`);
    this.name = 'ProblemError';
    this.status = status;
    /** @type {object|undefined} */ this.body = body;
    /** @type {Response|undefined} */ this.response = response;
  }
}

/**
 * A thin client over the KYC service. Construct one and share it.
 */
export class KycClient {
  /**
   * @param {{ baseUrl?: string, fetch?: typeof fetch, credentials?: RequestCredentials, signal?: AbortSignal }} [options]
   *   `baseUrl` defaults to '' (same origin — the KYC host serves this app alongside its API).
   */
  constructor(options = {}) {
    /** @private */ this._baseUrl = String(options.baseUrl ?? '').replace(/\/+$/, '');
    /** @private */ this._fetch = options.fetch;
    /** @private */ this._credentials = options.credentials ?? 'same-origin';
    /** @private */ this._signal = options.signal;
  }

  /**
   * `listVerifications` — one page of KYC verification records, newest first.
   * @param {{ limit?: number, pageToken?: string, signal?: AbortSignal }} [query]
   * @returns {Promise<{ verifications: object[], nextPageToken: (string|null) }>}
   */
  async listVerifications(query = {}) {
    const search = new URLSearchParams();
    if (query.limit != null) search.set('limit', String(query.limit));
    if (query.pageToken) search.set('pageToken', query.pageToken);
    const suffix = search.toString();
    const page = await this._request('GET', `/verifications${suffix ? `?${suffix}` : ''}`, undefined, query.signal);
    return { verifications: page.verifications ?? [], nextPageToken: page.nextPageToken ?? null };
  }

  /**
   * `submitVerdict` — the manual-recovery action: record an operator's verdict for an account whose review is pending.
   * The KYC service persists it and publishes it onto the bus, which resumes the workflow run awaiting it.
   * @param {string} accountId
   * @param {{ verified: boolean, score?: number, fullName?: string }} verdict
   * @param {{ signal?: AbortSignal }} [opts]
   * @returns {Promise<void>}
   */
  async submitVerdict(accountId, verdict, opts = {}) {
    await this._request('POST', `/accounts/${encodeURIComponent(accountId)}/kyc-verdict`, verdict, opts.signal);
  }

  /** @private */
  async _request(method, path, body, signal) {
    const fetchImpl = this._fetch ?? globalThis.fetch;
    const headers = { accept: 'application/json' };
    if (body !== undefined) headers['content-type'] = 'application/json';
    const response = await fetchImpl(`${this._baseUrl}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      credentials: this._credentials,
      signal: signal ?? this._signal,
    });
    if (!response.ok) {
      let errorBody;
      try {
        errorBody = await response.json();
      } catch {
        errorBody = undefined;
      }

      throw new ProblemError(response.status, errorBody, response);
    }

    if (response.status === 204 || response.status === 202) {
      return undefined;
    }

    return response.json();
  }
}
