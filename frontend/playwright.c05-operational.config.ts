import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: 'c05-registration-proof.spec.ts',
  timeout: 60_000,
  expect: { timeout: 12_000 },
  fullyParallel: false,
  retries: 0,
  reporter: 'line',
  use: {
    baseURL: 'http://127.0.0.1:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run start -- --proxy-config ../qa/c05_operational_identity_proxy.json',
    url: 'http://127.0.0.1:4200',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results'
});
