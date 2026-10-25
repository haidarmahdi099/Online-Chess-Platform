import "dotenv/config";
import { spawnSync } from "node:child_process";

const required = [
  "E2E_PLAYER1_USERNAME",
  "E2E_PLAYER1_PASSWORD",
  "E2E_PLAYER2_USERNAME",
  "E2E_PLAYER2_PASSWORD",
  "E2E_ADMIN_USERNAME",
  "E2E_ADMIN_PASSWORD",
  "E2E_PUZZLE_ID",
  "E2E_PUZZLE_SOLUTION",
  "E2E_PUZZLE_WRONG_MOVES",
];
const missing = required.filter((key) => !process.env[key]);

if (missing.length) {
  console.error(`Full E2E mode requires dedicated accounts and seeded fixtures: ${missing.join(", ")}`);
  process.exit(1);
}

const result = spawnSync("npx", ["playwright", "test"], {
  stdio: "inherit",
  shell: process.platform === "win32",
  env: { ...process.env, E2E_REQUIRE_FULL: "1" },
});

if (result.error) throw result.error;
process.exit(result.status ?? 1);
