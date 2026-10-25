export const frontendUrl = (process.env.E2E_FRONTEND_URL || "http://localhost:3000").replace(/\/$/, "");
export const apiUrl = (process.env.E2E_API_URL || "http://localhost:5272").replace(/\/$/, "");

export const player1 = {
  username: process.env.E2E_PLAYER1_USERNAME,
  password: process.env.E2E_PLAYER1_PASSWORD,
};

export const player2 = {
  username: process.env.E2E_PLAYER2_USERNAME,
  password: process.env.E2E_PLAYER2_PASSWORD,
};

export const admin = {
  username: process.env.E2E_ADMIN_USERNAME,
  password: process.env.E2E_ADMIN_PASSWORD,
};

export const hasPlayers = [player1, player2].every(({ username, password }) => username && password);
export const hasAdmin = Boolean(admin.username && admin.password);

export function puzzleFixture() {
  const id = process.env.E2E_PUZZLE_ID;
  const solution = process.env.E2E_PUZZLE_SOLUTION?.split(",").map((move) => move.trim()).filter(Boolean);
  const wrongMoves = process.env.E2E_PUZZLE_WRONG_MOVES?.split(",").map((move) => move.trim()).filter(Boolean);
  return id && solution?.length ? { id, solution, wrongMoves } : undefined;
}
