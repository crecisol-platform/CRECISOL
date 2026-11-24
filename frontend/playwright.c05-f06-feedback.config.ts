import { defineConfig, devices } from '@playwright/test';

/** F06 isolated Chromium: no production realm, credentials or network calls. */
export default defineConfig({
  testDir: './tests', testMatch: '**/c05-f06-context-feedback.spec.ts',
  timeout: 70_000, expect: { timeout: 15_000 },
  fullyParallel: false, retries: 0, reporter: 'line',
  use: { baseURL: 'http://127.0.0.1:4208', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium-f06-feedback', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'npx ng serve --host 127.0.0.1 --port 4208',
    url: 'http://127.0.0.1:4208', reuseExistingServer: false,
    timeout: 180_000, env: { CI: 'true' } },
  outputDir: 'test-results-f06-feedback'
});
