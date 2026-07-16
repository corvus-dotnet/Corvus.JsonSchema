# Live UX suite — the kit against the sample app's real infrastructure

The mock UX suite (`test/ux/`) drives `demo/index.html` against an in-memory mock that resets on
every page load. This suite drives the SAME kit components where the sample app serves them
(`samples/arazzo` Aspire composition, `wwwroot/index.html` at `/`, kit source at `/ui`): a real
control plane over real stores, Keycloak sign-in through the BFF (§16.3), server-enforced
authorization, the real §16.5.4 directory, and a real runner that has executed the seeded runs.

It validates the layers the mock cannot:

- the OIDC challenge round trip (redirect → Keycloak form → cookie session → back to the shell)
- cookie + X-CSRF plumbing on every API call
- real persistence (a created rule survives; a denied request stays denied)
- cross-identity flows as *actual different users*, not a persona dropdown
- server-side authorization (the observer's mutation is a real 403 rendered as a problem banner)
- the bounded `/count` endpoints against real row counts

## Running it

1. Start the composition (from `samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost`):

   ```
   aspire start --no-build --isolated --non-interactive
   ```

   (Build the demo solution first if needed. The control plane serves `http://localhost:8090/`.)

2. From `web/arazzo-control-plane-ui`:

   ```
   npm run test:live
   ```

   Point elsewhere with `ARAZZO_LIVE_BASE_URL`. The project only exists when `ARAZZO_LIVE_UX=1`,
   so `npm run test:ux` / CI never trip over a missing composition.

## Rules of the road (see live-helpers.js)

The backend is REAL and SHARED — not a fresh mock per `page.goto`:

- create under `uniq()` names and clean up what you create;
- assert RELATIVELY (contains / gained-one / status-changed), never seed-exact counts;
- one worker, tests in order (`--workers=1` is baked into the npm script);
- identities are Keycloak users (`LIVE_USERS`); a second identity needs a second browser context;
- start console-error watching AFTER `signIn` — the pre-login 401 bounce is expected noise, and a
  test that deliberately provokes a 4xx (the 403 test) skips the clean-console assertion.

The composition is reset-each-run (no durable volumes, realm re-imported), so a composition
restart restores the pristine seeded state if a failed run leaves residue behind.
