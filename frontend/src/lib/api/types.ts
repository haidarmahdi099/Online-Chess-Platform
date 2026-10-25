/**
 * Wire types. They mirror the backend DTOs field for field (camelCase on the wire) so the UI
 * never invents a shape. Where the backend returns bare GUIDs, the UI resolves usernames
 * through `useUsernames` (POST /users/lookup).
 */

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/* ---- Identity ---- */

export interface LoginResult {
  userId: string;
  username: string;
  accessToken: string;
  refreshToken: string;
}

export interface RegisterResult {
  userId: string;
  username: string;
  email: string;
  emailVerificationRequired: boolean;
}

export interface CurrentUser {
  userId: string;
  username: string;
  email: string;
  createdAt: string;
  role: string;
}

export interface PublicUser {
  userId: string;
  username: string;
  createdAt: string;
}

export interface UserRef {
  userId: string;
  username: string;
}

export interface FriendRequestIn {
  id: string;
  userId: string;
  username: string;
  createdAt: string;
}

export interface FriendRequestOut {
  id: string;
  userId: string;
  createdAt: string;
}

export interface Preferences {
  userId?: string;
  boardTheme: string;
  pieceTheme: string;
  soundEnabled: boolean;
  premovesEnabled: boolean;
  showOnlineStatus: boolean;
  allowChallenges: boolean;
  updatedAt?: string | null;
}

/* ---- Games ---- */

export type GameStatus = "Waiting" | "Live" | "Finished" | "Aborted";
export type RatingCategory = "chess" | "puzzle" | "bullet" | "blitz" | "rapid" | "classical";
export type TimeCategory = "bullet" | "blitz" | "rapid" | "classical";

export interface GameDetails {
  id: string;
  whitePlayerId: string;
  blackPlayerId: string;
  fen: string;
  status: GameStatus;
  winnerId: string | null;
  endReason: string | null;
  initialTimeSeconds: number;
  incrementSeconds: number;
  rated: boolean;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
}

export interface MoveHistoryItem {
  ply: number;
  moveNumber: number;
  color: string;
  from: string;
  to: string;
  promotion: string | null;
  san: string;
  fen: string;
  playedAt: string;
}

export interface GameMoves {
  gameId: string;
  initialFen: string;
  status: string;
  moves: MoveHistoryItem[];
}

export interface GameClock {
  gameId: string;
  whiteRemainingMilliseconds: number;
  blackRemainingMilliseconds: number;
  whiteToMove: boolean;
  turnStartedAt: string | null;
}

export interface ChatMessage {
  id: string;
  userId: string;
  message: string;
  createdAt: string;
}

export interface TimeControlBody {
  initialSeconds: number;
  incrementSeconds: number;
  category: TimeCategory;
}

export interface SeekResponse {
  seekId: string;
  status: string;
  initialSeconds: number;
  incrementSeconds: number;
  category: string;
  rated: boolean;
  createdAt: string;
  gameId: string | null;
}

export interface MoveResult {
  success: boolean;
  errorCode: string | null;
  errorMessage: string | null;
  fen: string | null;
  isCheck: boolean;
  isCheckmate: boolean;
  isStalemate: boolean;
  gameOver: boolean;
  endReason: string | null;
  drawOfferDeclined: boolean;
}

export interface SpectatorConsentResult {
  gameStarted: boolean;
  consentResolved: boolean;
  spectatorsAllowed: boolean;
  consentRejected: boolean;
}

export interface GameStats {
  totalGames: number;
  liveGames: number;
  finishedGames: number;
  wins: number;
  losses: number;
  draws: number;
}

export interface Challenge {
  id: string;
  challengerUserId: string;
  opponentUserId?: string;
  initialTimeSeconds: number;
  incrementSeconds: number;
  rated: boolean;
  status: string;
  gameId?: string | null;
  createdAtUtc: string;
  expiresAtUtc: string;
}

export interface Rematch {
  id: string;
  originalGameId: string;
  requesterUserId?: string;
  opponentUserId?: string;
  initialTimeSeconds: number;
  incrementSeconds: number;
  rated: boolean;
  status?: string;
  rematchGameId?: string | null;
  createdAtUtc: string;
  expiresAtUtc: string;
}

/* ---- Ratings ---- */

export interface RatingSummary {
  category: string;
  rating: number;
  gamesPlayed: number;
  wins: number;
  losses: number;
  draws: number;
}

export interface PlayerRatings {
  playerId: string;
  chess: RatingSummary;
  puzzle: RatingSummary;
  bullet: RatingSummary;
  blitz: RatingSummary;
  rapid: RatingSummary;
  classical: RatingSummary;
}

export interface RatingHistoryEntry {
  previousRating: number;
  newRating: number;
  change: number;
  reason: string;
  createdAtUtc: string;
}

export interface LeaderboardEntry {
  playerId: string;
  rating: number;
  gamesPlayed: number;
  wins: number;
  losses: number;
  draws: number;
  rank: number;
}

/* ---- Puzzles ---- */

export interface Puzzle {
  id: string;
  fen: string;
  initialMove: string | null;
  rating: number;
  themes: string[];
}

/** One row of the public puzzle catalogue (GET /puzzles). */
export interface PuzzleListItem {
  id: string;
  rating: number;
  themes: string[];
}

/** Result of submitting a whole solution at once (POST /puzzles/{id}/attempt). */
export interface PuzzleAttemptResult {
  puzzleId: string;
  isCorrect: boolean;
  message: string;
  ratingAffected: boolean;
}

export interface PuzzleStepResult {
  puzzleId: string;
  /** "continue" | "solved" | "failed" */
  status: string;
  opponentReply: string | null;
  solution: string[] | null;
  message: string;
  ratingAffected: boolean;
}

export interface PuzzleStats {
  userId: string;
  totalAttempts: number;
  correctAttempts: number;
  accuracy: number;
  distinctPuzzlesSolved: number;
  currentDailyStreak: number;
  lastAttemptAtUtc: string | null;
}

export interface PuzzleAttemptItem {
  id: string;
  puzzleId: string;
  isCorrect: boolean;
  attemptedAtUtc: string;
  submittedMoves: string[];
}

/* ---- Tournaments ---- */

export interface TournamentListItem {
  id: string;
  name: string;
  description: string | null;
  status: string;
  initialTimeSeconds: number;
  incrementSeconds: number;
  participantCount: number;
  maxParticipants: number;
  startsAtUtc: string;
}

export interface TournamentMatch {
  id: string;
  roundNumber: number;
  whitePlayerId: string;
  blackPlayerId: string | null;
  gameId: string | null;
  status: string;
  result: string | null;
  winnerId: string | null;
}

export interface TournamentDetails {
  id: string;
  name: string;
  description: string | null;
  status: string;
  startsAtUtc: string;
  createdByUserId: string;
  initialTimeSeconds: number;
  incrementSeconds: number;
  maxParticipants: number;
  participantIds: string[];
  matches: TournamentMatch[];
  totalRounds: number;
  currentRound: number;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
}

export interface TournamentStanding {
  rank: number;
  userId: string;
  points: number;
  gamesPlayed: number;
  wins: number;
  draws: number;
  losses: number;
  byes: number;
  buchholz: number;
}

export interface TournamentStandings {
  tournamentId: string;
  status: string;
  currentRound: number;
  totalRounds: number;
  standings: TournamentStanding[];
}

export interface TournamentMe {
  tournamentId: string;
  userId: string;
  isRegistered: boolean;
  isOwner: boolean;
  status: string;
  currentRound: number;
}

/** GET /tournaments/{id}/participants */
export interface TournamentParticipants {
  tournamentId: string;
  participantCount: number;
  participants: string[];
}

/** GET /tournaments/{id}/matches */
export interface TournamentMatches {
  tournamentId: string;
  currentRound: number;
  matches: TournamentMatch[];
}

/** GET /tournaments/{id}/rounds */
export interface TournamentRounds {
  tournamentId: string;
  currentRound: number;
  totalRounds: number;
  rounds: { roundNumber: number; matches: TournamentMatch[] }[];
}

/** GET /tournaments/{id}/results: only matches that already have a result. */
export interface TournamentResults {
  tournamentId: string;
  status: string;
  results: TournamentMatch[];
}

export interface JoinTournamentResult {
  tournamentId: string;
  outcome: string;
  joined: boolean;
  message: string;
  participantCount: number;
}

export interface CreateTournamentBody {
  name: string;
  description?: string | null;
  startsAtUtc: string;
  initialTimeSeconds: number;
  incrementSeconds: number;
  maxParticipants: number;
}

/* ---- Analysis ---- */

export interface Evaluation {
  fen: string;
  depth: number;
  /** "cp" (centipawns) or "mate" (moves to mate). Always from White's point of view. */
  scoreType: string;
  score: number;
  bestMove: string | null;
  principalVariation: string[];
}
