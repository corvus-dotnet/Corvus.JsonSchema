// Screen-capture config for the UX walkthrough clips (docs/control-plane/ux-review.md §8). Drives the in-browser
// demo (mock API, no server) and records a video per clip; a caption banner is injected per step by record.spec.js.
// Run: npx playwright test --config test/record.config.mjs ; clips land under recordings-raw/<title>/video.webm.
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '**/record.spec.js',
  reporter: 'list',
  timeout: 120000,
  use: {
    baseURL: 'http://localhost:8141',
    viewport: { width: 1280, height: 800 },
    video: { mode: 'on', size: { width: 1280, height: 800 } },
    // Slow each interaction enough to read the cursor + its effect, but small enough that a caption leads the change by
    // only a couple hundred ms (step()'s lead) — not a long pre-action wait. The post-action hold is what gives reading time.
    launchOptions: { slowMo: 250 },
  },
  // Serve the project root so the demo (/demo) and the kit it imports (/src) both resolve. A dedicated port +
  // reuseExistingServer:false avoids latching onto a stale http.server left on another port from a prior session.
  webServer: {
    command: 'python3 -m http.server 8141',
    cwd: '..',
    url: 'http://localhost:8141/demo/index.html',
    reuseExistingServer: false,
    timeout: 30000,
  },
  outputDir: '../recordings-raw',
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
