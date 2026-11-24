import { defineConfig, devices } from '@playwright/test';

/** F06 exhaustive F01 catalog: five viewports; isolated mocked browser realm. */
export default defineConfig({
  testDir: './tests', testMatch: '**/c05-f06-inventory-65.spec.ts',
  timeout: 90_000, expect: { timeout: 15_000 },
  fullyParallel: false, retries: 0, reporter: 'line',
  use: { baseURL: 'http://127.0.0.1:4212',
    trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium-f06-inventory65', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'npx ng serve --host 127.0.0.1 --port 4212',
    url: 'http://127.0.0.1:4212', reuseExistingServer: false,
    timeout: 180_000, env: { CI: 'true' } },
  outputDir: 'test-results-f06-inventory65'
});
