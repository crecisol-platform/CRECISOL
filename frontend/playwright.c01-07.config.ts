import { defineConfig, devices } from '@playwright/test';

// C01-07 runs ONLY on a temporary GitHub runner. The Angular dev-server proxy
// forwards a same-origin public read to a live loopback .NET/MySQL stack.
export default defineConfig({
  testDir: './tests',
  testMatch: 'c01-07.real-stack.spec.ts',
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
    command: 'npm run start -- --proxy-config ../qa/c01_07_proxy.json',
    url: 'http://127.0.0.1:4200',
    reuseExistingServer: false,
    timeout: 180_000,
    env: { CI: 'true' }
  },
  outputDir: 'test-results'
});
