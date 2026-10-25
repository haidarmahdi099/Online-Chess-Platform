import "dotenv/config";

const frontendUrl = (process.env.E2E_FRONTEND_URL || "http://localhost:3000").replace(/\/$/, "");
const apiUrl = (process.env.E2E_API_URL || "http://localhost:5272").replace(/\/$/, "");

async function check(name, url, accepts = (response) => response.ok) {
  let response;
  try {
    response = await fetch(url, { signal: AbortSignal.timeout(8_000) });
  } catch (error) {
    throw new Error(`${name} is unavailable at ${url}: ${error.message}`);
  }
  if (!accepts(response)) {
    throw new Error(`${name} returned HTTP ${response.status} at ${url}`);
  }
}

export default async function globalSetup() {
  await check("Frontend", frontendUrl);
  await check("Backend Gateway readiness", `${apiUrl}/health/ready`);

  if (process.env.E2E_REQUIRE_FULL === "1") {
    const missing = [
      "E2E_PLAYER1_USERNAME",
      "E2E_PLAYER1_PASSWORD",
      "E2E_PLAYER2_USERNAME",
      "E2E_PLAYER2_PASSWORD",
      "E2E_ADMIN_USERNAME",
      "E2E_ADMIN_PASSWORD",
      "E2E_PUZZLE_ID",
      "E2E_PUZZLE_SOLUTION",
      "E2E_PUZZLE_WRONG_MOVES",
    ].filter((key) => !process.env[key]);
    if (missing.length) {
      throw new Error(`Full E2E mode requires dedicated accounts and seeded fixtures: ${missing.join(", ")}`);
    }
  }
}
