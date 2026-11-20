import { defineConfig, devices } from '@playwright/test';

// F04: dedicated operator realm and five administrative views, synthetic API.
export default defineConfig({
  testDir: './tests', testMatch: '**/c05-f04-operators.spec.ts',
  timeout: 60_000, expect: { timeout: 12_000 }, fullyParallel: false,
  retries: 0, reporter: 'line',
  use: { baseURL: 'http://127.0.0.1:4206', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'chromium-f04-operators', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npx ng serve --host 127.0.0.1 --port 4206',
    url: 'http://127.0.0.1:4206', reuseExistingServer: false,
    timeout: 180_000, env: { CI: 'true' }
  },
  outputDir: 'test-results-f04-operators'
});
