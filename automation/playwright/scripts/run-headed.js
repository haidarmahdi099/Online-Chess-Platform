import "dotenv/config";
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

const playwrightCli = resolve("node_modules/playwright/cli.js");
const result = spawnSync(process.execPath, [playwrightCli, "test", "--headed", ...process.argv.slice(2)], {
  stdio: "inherit",
  shell: false,
  env: { ...process.env, E2E_SLOW_MO: process.env.E2E_SLOW_MO || "250" },
});

if (result.error) throw result.error;
process.exit(result.status ?? 1);
