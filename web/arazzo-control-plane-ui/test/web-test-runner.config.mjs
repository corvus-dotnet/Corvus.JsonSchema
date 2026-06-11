// Tier 3 — component tests in a real (headless Chromium) browser via @web/test-runner.
// Run from the project root so both src/ (the deliverable under test) and demo/ (the mock) are served.
import { playwrightLauncher } from '@web/test-runner-playwright';

export default {
  files: 'test/components/*.test.js',
  nodeResolve: true,
  browsers: [playwrightLauncher({ product: 'chromium' })],
  testFramework: { config: { timeout: '5000' } },
};
