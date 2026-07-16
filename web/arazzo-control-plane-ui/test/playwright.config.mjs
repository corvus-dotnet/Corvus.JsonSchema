// Tier 3 — end-to-end smoke of the real demo page in a real browser.
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  reporter: process.env.CI ? 'github' : 'list',
  // 10s assertion budget + 4 workers: with every ux file running in parallel, the in-browser
  // mock's simulated latency stretches under CPU contention — the assertions are unchanged, just
  // more patient, and the worker cap keeps seven Chromiums from starving each other.
  expect: { timeout: 10_000 },
  workers: 4,
  use: { baseURL: 'http://localhost:8138', trace: 'on-first-retry' },
  // Serve the project root so the demo (/demo) and the deliverable it imports (/src) both resolve; the
  // portable Node server also maps /ui/... → root (designer.html's absolute imports) — see smoke-server.mjs.
  // (A pure-Node server, not `python3 -m http.server`, so it runs on any OS runner without symlinks or python.)
  webServer: {
    command: 'node test/smoke-server.mjs',
    cwd: '..',
    url: 'http://localhost:8138/demo/index.html',
    reuseExistingServer: !process.env.CI,
    timeout: 30000,
  },
  // smoke.spec.js is the fast cross-cutting gate (fully parallel); test/ux/*.spec.js is the
  // comprehensive per-area UX suite — files run in parallel, tests WITHIN a file in order (the
  // in-browser mock's simulated latency makes same-file parallelism flaky under CPU contention).
  // record.spec.js is the clip recorder (run on demand via record.config.mjs) and never gates CI.
  projects: [
    { name: 'chromium', testMatch: '**/smoke.spec.js', fullyParallel: true, use: { ...devices['Desktop Chrome'] } },
    // One retry (with trace) absorbs cold-spawn contention flakes; a test that fails twice is real.
    { name: 'ux', testMatch: '**/ux/*.spec.js', fullyParallel: false, retries: 1, use: { ...devices['Desktop Chrome'] } },
  ],
});
