import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests/browser",
  use: { baseURL: "http://localhost:4187" },
  webServer: {
    command: "npm run preview -- --port 4187",
    url: "http://localhost:4187",
    reuseExistingServer: false,
  },
  workers: 1,
  reporter: "list",
});
