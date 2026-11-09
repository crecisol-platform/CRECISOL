import { defineConfig, devices } from '@playwright/test';

/** C02-07: only synthetic Angular component harness, never a public route. */
export default defineConfig({
  testDir: './tests',
  testMatch: '**/c02-07.patterns-mobile.spec.ts',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4201',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium-mobile-harness', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx ng serve --configuration pattern-harness --host 127.0.0.1 --port 4201',
    url: 'http://127.0.0.1:4201',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results-c02-07-harness'
});
