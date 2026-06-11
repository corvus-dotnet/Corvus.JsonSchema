# UI kit tests

Three tiers, fastest first. Run from the project root.

| Tier | What it checks | Command | Needs a browser? |
|------|----------------|---------|------------------|
| 1 | `ArazzoControlPlaneClient` behaviour against the in-memory mock (`client.test.mjs`) | `npm test` | no |
| 2 | The client's requests match the OpenAPI contract — guards against drift from the generated .NET client (`conformance.test.mjs`) | `npm test` | no |
| 3a | Components mounted in a real (headless Chromium) browser (`components/*.test.js`) | `npm run test:components` | yes |
| 3b | End-to-end smoke of the live demo page (`smoke.spec.js`) | `npm run test:smoke` | yes |

- `npm test` — tiers 1 + 2, dependency-free Node (`node --test`). Fast signal on every change.
- `npm run test:browser` — tiers 3a + 3b (`@web/test-runner` + Playwright). First time: `npx playwright install chromium` (on Linux also `npx playwright install-deps chromium`, or `sudo` it, to get the OS libraries).
- `npm run test:all` — everything.

Tiers 1–2 import the deliverable from `../src/` and the mock from `../demo/`; tier 2 reads
`docs/control-plane/arazzo-control-plane.openapi.json`. CI runs all tiers via
[`.github/workflows/ui.yml`](../../../.github/workflows/ui.yml).
