# Arazzo KYC console

A build-free web-component console for the KYC service's operators. It renders the paged list of identity
verifications and — for each **pending** one (an out-of-band review the `onboard-customer-async` workflow is
suspended awaiting) — an inline **submit verdict** form. Submitting `POST /accounts/{id}/kyc-verdict` publishes the
verdict onto the message bus, which resumes the suspended workflow run. Verified/blocked records show the resolved
identity (confidence, applicant, evidence, channel).

This is a **separate application** from the Arazzo control-plane UI: the KYC product ships independently and merely
consumes the workflow engine's effects. It has no build step — it is plain ES modules + a `<kyc-console>` custom
element (Shadow DOM) reading the KYC service through `KycClient`.

## Running

The KYC host serves this app at its root (same origin as the API), so under the Aspire AppHost just open the `kyc`
resource's endpoint. Standalone against a running KYC service:

```html
<link rel="stylesheet" href="src/kyc-kit.css">
<script type="module" src="src/kyc-console.js"></script>
<kyc-console poll="5"></kyc-console>
```

Attributes: `base-url` (default `''` — same origin), `limit` (page size, default 50), `poll` (auto-refresh seconds).

## Files

- `src/kyc-client.js` — a dependency-free client over `GET /verifications` (paged) and `POST /accounts/{id}/kyc-verdict`.
- `src/kyc-console.js` — the `<kyc-console>` custom element (list, filter, per-record detail, the verdict form).
- `src/kyc-kit.css` — the `--kyc-*` light/dark token kit.
