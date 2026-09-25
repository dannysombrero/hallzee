import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/deployment",
  use: { baseURL: "https://web.hallzee.com", trace: "off" },
  workers: 1,
  timeout: 90000,
  expect: { timeout: 15000 },
  reporter: "list",
});
