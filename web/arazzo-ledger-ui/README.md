# Reconciliation console

A small, build-free web app — the ledger product's operator console. It lists the nightly-reconcile runs and shows
each one's discrepancies (accounts whose ledger and bank balances disagree, with a severity), counts, and report,
reading the ledger service's `GET /reconciliations` and `GET /reconciliations/{runId}`.

This is a **separate application** from the Arazzo workflow engine. In reality it ships independently and merely
*consumes* the engine: the `nightly-reconcile` workflow the control plane orchestrates drives the ledger service, and
this console renders the resulting reconciliation runs. Here it is served by the ledger host itself (same origin as
its API), so it needs no cross-origin configuration.

## Layout

- `src/ledger-client.js` — a dependency-free ES-module client over the ledger read API (`LedgerClient`, `ProblemError`).
- `src/ledger-console.js` — the `<reconciliation-console>` custom element (Shadow DOM): the paged run list, per run the
  expandable discrepancy table (account · delta · currency · severity) plus counts, report URL, and published state,
  and optional polling.
- `src/ledger-kit.css` — self-contained `--ldg-*` theme tokens (light/dark, severity colours).
- `index.html` — mounts `<reconciliation-console>`.

## Running

Served at the ledger host's root (`/`) in the Arazzo control-plane sample's Aspire composition. Open the ledger
resource's endpoint from the dashboard.
