import { defineConfig, devices } from '@playwright/test';

// Corrected F03 contract: 6 authenticated account areas, synthetic fixtures only.
export default defineConfig({
  testDir: './tests',
  testMatch: '**/c05-f03-account.spec.ts',
  timeout: 60_000,
  expect: { timeout: 12_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4204',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium-f03-account', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx ng serve --host 127.0.0.1 --port 4204',
    url: 'http://127.0.0.1:4204',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results-f03-account'
});
