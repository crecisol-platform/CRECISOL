import { defineConfig, devices } from '@playwright/test';

/** F05 only, isolated Angular 22 loopback and synthetic responses. */
export default defineConfig({
  testDir: './tests', testMatch: '**/c05-f05-secure-modals.spec.ts',
  timeout: 65_000, expect: { timeout: 12_000 }, fullyParallel: false,
  retries: 0, reporter: 'line',
  use: { baseURL: 'http://127.0.0.1:4207', trace: 'retain-on-failure',
    screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium-f05-secure-modal', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'npx ng serve --host 127.0.0.1 --port 4207',
    url: 'http://127.0.0.1:4207', reuseExistingServer: false,
    timeout: 180_000, env: { CI: 'true' } },
  outputDir: 'test-results-f05-modals'
});
