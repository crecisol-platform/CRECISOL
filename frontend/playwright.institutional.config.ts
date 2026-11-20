import { defineConfig, devices } from '@playwright/test';

// Never a production URL. Isolated Angular 22 synthetic component laboratory.
export default defineConfig({
  testDir: './tests',
  testMatch: '**/c05-f02-design-system.spec.ts',
  timeout: 50_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4202',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium-institutional', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx ng serve --configuration institutional-harness --host 127.0.0.1 --port 4202',
    url: 'http://127.0.0.1:4202',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results-institutional'
});
