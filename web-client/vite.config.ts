import { readFileSync } from "node:fs";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
const headers = Object.fromEntries(
  readFileSync(new URL("./public/_headers", import.meta.url), "utf8")
    .split("/assets/*")[0]
    .split("\n")
    .filter((line) => line.startsWith("  "))
    .map((line) => {
      const i = line.indexOf(":");
      return [line.slice(0, i).trim(), line.slice(i + 1).trim()];
    }),
);
export default defineConfig({
  preview: { headers },
  plugins: [react(), tailwindcss()],
  build: { target: "chrome116", sourcemap: false },
});
