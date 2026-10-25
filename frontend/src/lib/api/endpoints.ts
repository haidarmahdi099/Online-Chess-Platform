import { http } from "@/lib/api/http";
import type {
  ChatMessage,
  Challenge,
  CreateTournamentBody,
  CurrentUser,
  Evaluation,
  FriendRequestIn,
  FriendRequestOut,
  GameClock,
  GameDetails,
  GameMoves,
  GameStats,
  JoinTournamentResult,
  LeaderboardEntry,
  LoginResult,
  Paged,
  PlayerRatings,
  Preferences,
  PublicUser,
  Puzzle,
  PuzzleAttemptItem,
  PuzzleAttemptResult,
  PuzzleListItem,
  PuzzleStats,
  PuzzleStepResult,
  RatingHistoryEntry,
  RegisterResult,
  Rematch,
  SeekResponse,
  SpectatorConsentResult,
  TimeControlBody,
  TournamentDetails,
  TournamentListItem,
  TournamentMatches,
  TournamentMe,
  TournamentParticipants,
  TournamentResults,
  TournamentRounds,
  TournamentStandings,
  UserRef,
} from "@/lib/api/types";

export const authApi = {
  register: (body: { username: string; email: string; password: string }) =>
    http.post<RegisterResult>("/auth/register", body, { auth: false }),
  login: (body: { usernameOrEmail: string; password: string }) =>
    http.post<LoginResult>("/auth/login", body, { auth: false }),
  logout: (refreshToken: string) =>
    http.post<void>("/auth/logout", { refreshToken }, { auth: false }),
  logoutEverywhere: () => http.post<void>("/auth/logout-everywhere"),
  verifyEmail: (body: { email: string; code: string }) =>
    http.post<void>("/auth/verify-email", body, { auth: false }),
  resendVerification: (email: string) =>
    http.post<void>("/auth/resend-verification", { email }, { auth: false }),
  forgotPassword: (email: string) =>
    http.post<void>("/auth/forgot-password", { email }, { auth: false }),
  resetPassword: (body: { email: string; token: string; newPassword: string }) =>
    http.post<void>("/auth/reset-password", body, { auth: false }),
  changePassword: (body: { currentPassword: string; newPassword: string }) =>
    http.post<void>("/auth/change-password", body),
  changeEmail: (body: { newEmail: string; password: string }) =>
    http.post<void>("/auth/change-email", body),
  confirmEmailChange: (token: string) =>
    http.post<void>("/auth/confirm-email-change", { token }, { auth: false }),
};

export const usersApi = {
  me: () => http.get<CurrentUser>("/users/me"),
  byUsername: (username: string) => http.get<PublicUser>(`/users/${encodeURIComponent(username)}`),
  lookup: (userIds: string[]) =>
    http.post<UserRef[]>("/users/lookup", { userIds }, { auth: false }),
  search: (q: string, limit = 20) =>
    http.get<PublicUser[]>("/users/search", { query: { q, limit } }),
  updateProfile: (username: string) => http.put<PublicUser>("/users/me/profile", { username }),
  deleteAccount: (password: string) => http.del<void>("/users/me", { password }),
};

export const socialApi = {
  friends: () => http.get<PublicUser[]>("/social/friends"),
  incoming: () => http.get<FriendRequestIn[]>("/social/friend-requests/incoming"),
  outgoing: () => http.get<FriendRequestOut[]>("/social/friend-requests/outgoing"),
  sendRequest: (userId: string) => http.post<unknown>("/social/friend-requests", { userId }),
  accept: (id: string) => http.post<unknown>(`/social/friend-requests/${id}/accept`),
  decline: (id: string) => http.post<unknown>(`/social/friend-requests/${id}/decline`),
  removeFriend: (userId: string) => http.del<void>(`/social/friends/${userId}`),
  blocks: () => http.get<PublicUser[]>("/social/blocks"),
  block: (userId: string) => http.post<void>(`/social/blocks/${userId}`),
  unblock: (userId: string) => http.del<void>(`/social/blocks/${userId}`),
  preferences: () => http.get<Preferences>("/social/preferences"),
  savePreferences: (body: Omit<Preferences, "userId" | "updatedAt">) =>
    http.put<Preferences>("/social/preferences", body),
};

export const gamesApi = {
  seek: (timeControl: TimeControlBody, rated: boolean) =>
    http.post<SeekResponse>("/games/seek", { timeControl, rated }),
  seekStatus: () => http.get<SeekResponse>("/games/seek/status"),
  cancelSeek: () => http.del<void>("/games/seek"),
  list: (filter: "mine" | "live", page = 1, pageSize = 20) =>
    http.get<Paged<GameDetails>>("/games", { query: { filter, page, pageSize } }),
  get: (id: string) => http.get<GameDetails>(`/games/${id}`),
  getPublic: (id: string) => http.get<GameDetails>(`/games/${id}/public`),
  moves: (id: string) => http.get<GameMoves>(`/games/${id}/moves`),
  pgn: (id: string) => http.get<string>(`/games/${id}/pgn`, { as: "text" }),
  clock: (id: string) => http.get<GameClock>(`/games/${id}/clock`),
  chat: (id: string) => http.get<ChatMessage[]>(`/games/${id}/chat`),
  abort: (id: string) => http.post<unknown>(`/games/${id}/abort`),
  respondConsent: (id: string, allowsSpectators: boolean) =>
    http.put<SpectatorConsentResult>(`/games/${id}/spectator-consent`, { allowsSpectators }),
  stats: () => http.get<GameStats>("/stats"),
  playerStats: (playerId: string) =>
    http.get<{ playerId: string; stats: GameStats }>(`/stats/${playerId}`, { auth: false }),
};

export const challengesApi = {
  create: (body: {
    opponentUserId: string;
    initialTimeSeconds: number;
    incrementSeconds: number;
    rated: boolean;
  }) => http.post<Challenge>("/challenges", body),
  /** One challenge (either side). Polled to learn when a sent challenge is accepted or declined. */
  get: (id: string) => http.get<Challenge>(`/challenges/${id}`),
  incoming: () => http.get<Challenge[]>("/challenges/incoming"),
  outgoing: () => http.get<Challenge[]>("/challenges/outgoing"),
  accept: (id: string) =>
    http.post<{ id: string; status: string; gameId: string }>(`/challenges/${id}/accept`),
  decline: (id: string) => http.post<unknown>(`/challenges/${id}/decline`),
  cancel: (id: string) => http.del<void>(`/challenges/${id}`),
};

export const rematchApi = {
  request: (gameId: string) => http.post<Rematch>(`/games/${gameId}/rematch`),
  incoming: () => http.get<Rematch[]>("/games/rematches/incoming"),
  outgoing: () => http.get<Rematch[]>("/games/rematches/outgoing"),
  accept: (id: string) =>
    http.post<{ id: string; status: string; originalGameId: string; rematchGameId: string }>(
      `/games/rematches/${id}/accept`,
    ),
  decline: (id: string) => http.post<unknown>(`/games/rematches/${id}/decline`),
  cancel: (id: string) => http.del<void>(`/games/rematches/${id}`),
};

export const ratingsApi = {
  player: (playerId: string) => http.get<PlayerRatings>(`/ratings/${playerId}`, { auth: false }),
  history: (playerId: string, category: string, page = 1, pageSize = 50) =>
    http.get<Paged<RatingHistoryEntry>>(`/ratings/${playerId}/history`, {
      auth: false,
      query: { category, page, pageSize },
    }),
  leaderboard: (category: string, page = 1, pageSize = 50) =>
    http.get<Paged<LeaderboardEntry>>("/leaderboard", {
      auth: false,
      query: { category, page, pageSize },
    }),
};

export const puzzlesApi = {
  /** Public catalogue, optionally limited to a rating range. */
  list: (page = 1, pageSize = 20, minRating?: number, maxRating?: number) =>
    http.get<Paged<PuzzleListItem>>("/puzzles", {
      auth: false,
      query: { page, pageSize, minRating, maxRating },
    }),
  next: (excludePuzzleId?: string) =>
    http.get<Puzzle>("/puzzles/next", { query: { excludePuzzleId } }),
  step: (id: string, moves: string[]) =>
    http.post<PuzzleStepResult>(`/puzzles/${id}/step`, { moves }),
  /** Submit a complete solution (UCI moves) in one request instead of step by step. */
  attempt: (id: string, moves: string[]) =>
    http.post<PuzzleAttemptResult>(`/puzzles/${id}/attempt`, { moves }),
  myStats: () => http.get<PuzzleStats>("/puzzles/me/stats"),
  myHistory: (page = 1, pageSize = 20) =>
    http.get<Paged<PuzzleAttemptItem>>("/puzzles/me/history", { query: { page, pageSize } }),
};

export const tournamentsApi = {
  list: (status?: string, page = 1, pageSize = 20) =>
    http.get<Paged<TournamentListItem>>("/tournaments", {
      auth: false,
      query: { status, page, pageSize },
    }),
  get: (id: string) => http.get<TournamentDetails>(`/tournaments/${id}`, { auth: false }),
  standings: (id: string) =>
    http.get<TournamentStandings>(`/tournaments/${id}/standings`, { auth: false }),
  participants: (id: string) =>
    http.get<TournamentParticipants>(`/tournaments/${id}/participants`, { auth: false }),
  matches: (id: string) =>
    http.get<TournamentMatches>(`/tournaments/${id}/matches`, { auth: false }),
  rounds: (id: string) => http.get<TournamentRounds>(`/tournaments/${id}/rounds`, { auth: false }),
  results: (id: string) =>
    http.get<TournamentResults>(`/tournaments/${id}/results`, { auth: false }),
  me: (id: string) => http.get<TournamentMe>(`/tournaments/${id}/me`),
  create: (body: CreateTournamentBody) => http.post<TournamentDetails>("/tournaments", body),
  join: (id: string) => http.post<JoinTournamentResult>(`/tournaments/${id}/join`),
  leave: (id: string) => http.del<unknown>(`/tournaments/${id}/leave`),
  start: (id: string) => http.post<unknown>(`/tournaments/${id}/start`),
  cancel: (id: string) => http.post<unknown>(`/tournaments/${id}/cancel`),
  complete: (id: string) => http.post<unknown>(`/tournaments/${id}/complete`),
  pairings: (id: string) => http.post<unknown>(`/tournaments/${id}/pairings`),
  retryProvisioning: (id: string) => http.post<unknown>(`/tournaments/${id}/provisioning/retry`),
  setResult: (id: string, matchId: string, result: "WhiteWin" | "BlackWin" | "Draw") =>
    http.put<unknown>(`/tournaments/${id}/matches/${matchId}/result`, { result }),
};

export const analysisApi = {
  evaluate: (fen: string, depth = 14, signal?: AbortSignal) =>
    http.post<Evaluation>("/analysis/evaluate", { fen, depth }, signal ? { signal } : undefined),
};
