import { defineConfig, devices } from '@playwright/test';

// F03 runs the local C05 page with synthetic API answers only. No real accounts,
// external requests, production deployment, or tenant-specific host redirects.
export default defineConfig({
  testDir: './tests',
  testMatch: '**/c05-f03-navigation.spec.ts',
  timeout: 60_000,
  expect: { timeout: 12_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4203',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium-c05-f03', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx ng serve --host 127.0.0.1 --port 4203',
    url: 'http://127.0.0.1:4203',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results-f03'
});
