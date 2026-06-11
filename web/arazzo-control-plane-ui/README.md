# @corvus-dotnet/arazzo-control-plane-ui

A zero-build, framework-agnostic kit of composable **web components** over the
[Arazzo Control Plane REST API](../../docs/control-plane/README.md). Drop the pieces you need into any web app
— React, Vue, Svelte, Angular, or plain HTML — with no toolchain. See
[`ui-design.md`](../../docs/control-plane/ui-design.md) for the full design.

## Install

```bash
npm install @corvus-dotnet/arazzo-control-plane-ui
```

Or use it straight from a CDN (no install) — every module is plain ESM:

```html
<script type="module"
  src="https://cdn.jsdelivr.net/npm/@corvus-dotnet/arazzo-control-plane-ui/src/arazzo-control-plane.js"></script>
```

The kit is **layered** so you adopt at any granularity (paths shown for direct/CDN use; with a bundler use the
package `exports` — `@corvus-dotnet/arazzo-control-plane-ui`, `.../client`, `.../components/runs-table.js`):

- **Layer 0 — `ArazzoControlPlaneClient`** ([`src/arazzo-client.js`](./src/arazzo-client.js)): a DOM-free
  client for the run **and** catalog operations. Useful on its own (Node, tests, CLIs, any UI).
- **Layer 1 — standalone components** ([`src/components/`](./src/components)): `<arazzo-runs-table>`,
  `<arazzo-run-detail>`, `<arazzo-resume-dialog>`, `<arazzo-cancel-button>`, `<arazzo-purge-dialog>`,
  `<arazzo-status-badge>` for runs; `<arazzo-catalog-table>`, `<arazzo-catalog-detail>`,
  `<arazzo-catalog-add-dialog>` for the workflow catalog. Each works alone. The add dialog can build the
  package archive in-browser from a workflow + its sources (`./workflow-package` ·
  [`src/workflow-package.js`](./src/workflow-package.js)) or upload a pre-built one; the catalog assigns the
  version number server-side.
- **Layer 2 — `<arazzo-control-plane>`** ([`src/arazzo-control-plane.js`](./src/arazzo-control-plane.js)) and
  **`<arazzo-catalog>`** ([`src/arazzo-catalog.js`](./src/arazzo-catalog.js)): the reference panels that
  compose Layer 1 into a run-management screen and a catalog browse/govern screen respectively.

## Quick start

Open the [`demo/`](./demo) — it runs the whole panel against an in-memory mock, no server needed. The demo is
dev-only (it is **not** part of the published package); it imports the deliverable from `../src/`, so serve the
project root over HTTP (both `demo/` and `src/` must be reachable):

```bash
cd web/arazzo-control-plane-ui
python3 -m http.server 8137   # then browse to http://localhost:8137/demo/
```

In your own app (bundler — uses the package `exports`):

```js
import '@corvus-dotnet/arazzo-control-plane-ui';            // registers all elements (incl. the panel)
import '@corvus-dotnet/arazzo-control-plane-ui/kit.css';    // optional: app-wide theme + dark mode
```
```html
<arazzo-control-plane base-url="/arazzo/v1" scopes="runs:read runs:write" theme="auto"></arazzo-control-plane>
<script type="module">
  // The host owns its OAuth2/OIDC/mTLS session and hands the kit credentials.
  document.querySelector('arazzo-control-plane').authProvider =
    async () => `Bearer ${await myApp.getAccessToken()}`;
</script>
```

Or compose just the pieces you want:

```js
import { ArazzoControlPlaneClient } from '@corvus-dotnet/arazzo-control-plane-ui/client';
import '@corvus-dotnet/arazzo-control-plane-ui/components/runs-table.js';

const client = new ArazzoControlPlaneClient({ baseUrl: '/arazzo/v1', getAuthHeader: () => `Bearer ${token}` });
const table = document.querySelector('arazzo-runs-table');
table.client = client;
table.addEventListener('run-selected', (e) => console.log(e.detail.run));
```

### Serving from a .NET (or any) host

The kit is just static ESM, so any server can host it: copy `src/` to your static root, reference it from a
CDN, or (for an ASP.NET host) drop the files under the host's `wwwroot`. There is no .NET package — the
deliverable is the npm package.

## Auth

The kit never embeds an IdP flow. Configure credentials one of three ways (precedence top to bottom):

| Option | How | When |
|--------|-----|------|
| `fetch` hook | `new ArazzoControlPlaneClient({ baseUrl, fetch })` / `panel.fetch = …` | full control: interceptors, retries, mTLS host |
| `getAuthHeader` / `authProvider` | returns the `Authorization` header value per request | bearer tokens from the host's session |
| same-origin credentials | default (`credentials: 'include'`) | a reverse proxy injects cookie/mTLS auth |

## Components at a glance

| Element | Key attributes | Events |
|---------|----------------|--------|
| `<arazzo-runs-table>` | `base-url`, `status`, `workflow-id`, `created-after`, `created-before`, `updated-after`, `updated-before`, `page-size`, `poll`, `selectable` | `run-selected`, `loaded`, `error` |
| `<arazzo-run-detail>` | `base-url`, `runid`, `poll`, `scopes`, `show-forbidden` | `run-changed`, `run-deleted`, `error`, `close` |
| `<arazzo-resume-dialog>` | (`.client`, `.open(run)`) | `resume-submitted`, `error` |
| `<arazzo-cancel-button>` | `base-url`, `runid`, `label`, `disabled`, `no-confirm` | `run-cancelled`, `error` |
| `<arazzo-purge-dialog>` | (`.client`, `.open()`) | `purge-completed`, `error` |
| `<arazzo-status-badge>` | `status` | — |
| `<arazzo-control-plane>` | `base-url`, `scopes`, `theme`, `poll` | re-emits the above |

The runs table / panel filter server-side by status, workflowId, and a **time window** — `created-after` /
`created-before` / `updated-after` / `updated-before` (RFC 3339 instants; `after` inclusive, `before`
exclusive). Every data component renders explicit **loading / empty / error** states; dialogs are
focus-trapped native `<dialog>`s; destructive actions (delete, purge) require confirmation; and actions are
**hidden unless the `scopes` allow them** (pass `show-forbidden` to show them disabled instead).

## Theming

Components read `--arazzo-*` custom properties (which inherit through Shadow DOM) with light-mode fallbacks.
Theme the whole kit by either including [`src/arazzo-kit.css`](./src/arazzo-kit.css) at `:root` (adds dark mode
via `prefers-color-scheme`, or force it with `data-theme="dark|light"` on a host element) or setting
`theme="light|dark|auto"` on `<arazzo-control-plane>`. Override any token in your own CSS to restyle without
forking; structural nodes expose `::part()`s for deeper changes.

```css
:root { --arazzo-accent: #7c3aed; --arazzo-radius: 12px; }
arazzo-runs-table::part(row):hover { outline: 1px solid var(--arazzo-accent); }
```

## Files

The published package is **`src/` only** (`package.json` `files`); `demo/` and `test/` are dev-only.

```
src/                        ← DELIVERABLE (published to npm)
  arazzo-client.js          Layer 0 client (ArazzoControlPlaneClient, ProblemError)
  arazzo-control-plane.js   Layer 2 panel (registers everything)
  arazzo-kit.css            optional theme tokens
  components/                Layer 1 elements + shared base.js
demo/                       ← DEV-ONLY sample: index.html + mock-api.js (in-memory control plane)
test/                       ← DEV-ONLY: node:test (client + OpenAPI conformance), @web/test-runner, Playwright
```

Everything is loose ES modules — no bundler, no transpile. Import one file or the whole panel, from npm, a
CDN, or your own static host.

## Tests

See [`test/README.md`](./test/README.md). `npm test` runs the dependency-free Node tiers (client behaviour +
conformance against `arazzo-control-plane.openapi.json`); `npm run test:browser` runs the component and smoke
tests in headless Chromium (run `npx playwright install chromium` once first; on Linux also
`npx playwright install-deps chromium`). CI runs all of it via
[`.github/workflows/ui.yml`](../../.github/workflows/ui.yml).
