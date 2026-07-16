// Tier 3 — end-to-end smoke of the real demo page in a real browser.
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  // The smoke gate covers smoke.spec.js only; record.spec.js is the clip recorder (run on demand via record.config.mjs),
  // not a smoke test — a hangover from clip generation that should not gate CI.
  testMatch: '**/smoke.spec.js',
  fullyParallel: true,
  reporter: process.env.CI ? 'github' : 'list',
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
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
