// Tier 3 — end-to-end smoke of the real demo page in a real browser.
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '**/*.spec.js',
  fullyParallel: true,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL: 'http://localhost:8138', trace: 'on-first-retry' },
  // Serve the project root so the demo (/demo) and the deliverable it imports (/src) both resolve.
  webServer: {
    command: 'python3 -m http.server 8138',
    cwd: '..',
    url: 'http://localhost:8138/demo/index.html',
    reuseExistingServer: !process.env.CI,
    timeout: 30000,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
