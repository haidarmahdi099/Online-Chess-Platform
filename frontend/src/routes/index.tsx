import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { motion, useReducedMotion } from "motion/react";
import { ArrowRight, Puzzle, Radio, Swords, Trophy, Users } from "lucide-react";
import heroBoard from "@/assets/hero-board.jpg";
import { Avatar, Pill } from "@/components/common/bits";
import { Reveal, Stagger, StaggerItem } from "@/components/common/motion";
import { MiniBoard } from "@/components/game/MiniBoard";
import { Button } from "@/components/ui/button";
import { gamesApi, ratingsApi } from "@/lib/api/endpoints";
import { useUsernames } from "@/lib/api/hooks";
import { gameCategory, timeControlLabel } from "@/lib/chess-utils";
import { useAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/")({
  head: () => ({ meta: [{ title: "Echelon Chess — Play, train, compete" }] }),
  component: Home,
});

const FEATURES = [
  {
    icon: Swords,
    title: "Rated & casual play",
    text: "Bullet to classical with instant matchmaking and server-side clocks.",
    to: "/play",
    tone: "text-primary bg-primary/15 group-hover:border-primary/60",
  },
  {
    icon: Puzzle,
    title: "Tactics training",
    text: "Rated puzzles with step-by-step validation and a daily streak.",
    to: "/puzzles",
    tone: "text-violet bg-violet/15 group-hover:border-violet/60",
  },
  {
    icon: Trophy,
    title: "Tournaments",
    text: "Swiss events with live standings, pairings and results.",
    to: "/tournaments",
    tone: "text-gold bg-gold/15 group-hover:border-gold/60",
  },
  {
    icon: Radio,
    title: "Watch live",
    text: "Follow games move by move with a built-in Stockfish evaluation.",
    to: "/watch",
    tone: "text-teal bg-teal/15 group-hover:border-teal/60",
  },
] as const;

/** Decorative pieces drifting behind the hero. Hidden from assistive tech, off with reduced motion. */
const FLOATERS = [
  { g: "♞", x: "8%", y: "18%", s: "5rem", d: 7 },
  { g: "♛", x: "84%", y: "14%", s: "6.5rem", d: 9 },
  { g: "♜", x: "74%", y: "62%", s: "4.5rem", d: 8 },
  { g: "♝", x: "14%", y: "70%", s: "4rem", d: 10 },
] as const;

function Home() {
  const { status, user } = useAuth();
  const reduce = useReducedMotion();
  const top = useQuery({
    queryKey: ["leaderboard", "chess", 1, 5],
    queryFn: () => ratingsApi.leaderboard("chess", 1, 5),
    staleTime: 60_000,
  });
  const live = useQuery({
    queryKey: ["games", "live", "home"],
    queryFn: () => gamesApi.list("live", 1, 3),
    enabled: status === "authenticated",
    refetchInterval: 15_000,
  });
  const { name } = useUsernames([
    ...(top.data?.items ?? []).map((e) => e.playerId),
    ...(live.data?.items ?? []).flatMap((g) => [g.whitePlayerId, g.blackPlayerId]),
  ]);

  return (
    <div>
      <section className="relative isolate overflow-hidden hero-bg">
        <div className="pointer-events-none absolute inset-0 -z-10">
          <img src={heroBoard} alt="" className="h-full w-full object-cover opacity-30" />
          <div className="aurora" />
          <div className="absolute inset-0 bg-gradient-to-b from-background/60 via-background/50 to-background" />
        </div>

        {!reduce && (
          <div aria-hidden className="pointer-events-none absolute inset-0 -z-10 hidden sm:block">
            {FLOATERS.map((f) => (
              <motion.span
                key={f.g}
                className="absolute select-none text-foreground/10"
                style={{ left: f.x, top: f.y, fontSize: f.s }}
                animate={{ y: [0, -18, 0], rotate: [-4, 4, -4] }}
                transition={{ duration: f.d, repeat: Infinity, ease: "easeInOut" }}
              >
                {f.g}
              </motion.span>
            ))}
          </div>
        )}

        <Stagger className="mx-auto max-w-[1400px] px-4 pb-16 pt-12 sm:px-6 sm:pb-28 sm:pt-24">
          <StaggerItem>
            <p className="label-mono">
              {user ? `Welcome back, ${user.username}` : "Online chess, built for serious play"}
            </p>
          </StaggerItem>
          <StaggerItem>
            <h1 className="mt-4 max-w-4xl font-display text-[clamp(2.4rem,9vw,6rem)] font-bold uppercase leading-[0.95]">
              Outthink every
              <br />
              <span className="text-gradient">opponent</span>
            </h1>
          </StaggerItem>
          <StaggerItem>
            <p className="mt-5 max-w-xl text-base text-muted-foreground sm:mt-6">
              Rated games, live tournaments, daily puzzles and engine analysis, all on one fast,
              fair platform.
            </p>
          </StaggerItem>
          <StaggerItem>
            <div className="mt-7 flex flex-col gap-3 sm:mt-8 sm:flex-row sm:flex-wrap">
              <Button asChild size="lg" className="font-mono uppercase tracking-[0.14em] glow">
                <Link to="/play">
                  Play now <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              </Button>
              {status === "anonymous" ? (
                <Button
                  asChild
                  size="lg"
                  variant="outline"
                  className="font-mono uppercase tracking-[0.14em]"
                >
                  <Link to="/register">Create free account</Link>
                </Button>
              ) : (
                <Button
                  asChild
                  size="lg"
                  variant="outline"
                  className="font-mono uppercase tracking-[0.14em]"
                >
                  <Link to="/puzzles">Solve a puzzle</Link>
                </Button>
              )}
            </div>
          </StaggerItem>
        </Stagger>
      </section>

      <section className="mx-auto max-w-[1400px] px-4 py-12 sm:px-6">
        <Stagger inView className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {FEATURES.map((f) => (
            <StaggerItem key={f.title} className="h-full">
              <Link to={f.to} className="panel lift group block h-full p-5 sm:p-6">
                <span
                  className={`grid h-11 w-11 place-items-center rounded-lg transition-transform group-hover:scale-110 ${f.tone}`}
                >
                  <f.icon className="h-5 w-5" />
                </span>
                <h2 className="mt-5 font-display text-lg font-bold uppercase">{f.title}</h2>
                <p className="mt-2 text-sm text-muted-foreground">{f.text}</p>
              </Link>
            </StaggerItem>
          ))}
        </Stagger>
      </section>

      <section className="mx-auto grid max-w-[1400px] gap-6 px-4 pb-20 sm:px-6 lg:grid-cols-2">
        <Reveal className="panel p-5 sm:p-6">
          <div className="flex items-center justify-between">
            <h2 className="flex items-center gap-2 font-display text-xl font-bold uppercase">
              <Trophy className="h-5 w-5 text-gold" /> Top players
            </h2>
            <Link
              to="/leaderboard"
              search={{ category: "chess" }}
              className="label-mono hover:text-foreground"
            >
              Full board →
            </Link>
          </div>
          {top.data && top.data.items.length > 0 ? (
            <ul className="mt-4 divide-y divide-border">
              {top.data.items.map((e) => (
                <li key={e.playerId} className="flex items-center gap-3 py-3">
                  <span className="w-5 font-mono text-sm text-muted-foreground">{e.rank}</span>
                  <Avatar name={name(e.playerId)} className="h-8 w-8 text-[10px]" />
                  <Link
                    to="/players/$username"
                    params={{ username: name(e.playerId) }}
                    className="min-w-0 flex-1 truncate text-sm font-semibold hover:text-primary hover:underline"
                  >
                    {name(e.playerId)}
                  </Link>
                  <span className="font-mono text-sm font-bold tabular-nums">{e.rating}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-6 text-sm text-muted-foreground">
              {top.isLoading ? "Loading…" : "No ranked players yet. Be the first."}
            </p>
          )}
        </Reveal>

        <Reveal delay={0.08} className="panel p-5 sm:p-6">
          <div className="flex items-center justify-between">
            <h2 className="flex items-center gap-2 font-display text-xl font-bold uppercase">
              <Radio className="h-5 w-5 text-live" /> Live now
            </h2>
            <Link to="/watch" className="label-mono hover:text-foreground">
              All games →
            </Link>
          </div>
          {status !== "authenticated" ? (
            <div className="mt-6 text-sm text-muted-foreground">
              <Users className="mb-3 h-6 w-6" />
              <Link
                to="/login"
                search={{ redirect: "/watch" }}
                className="font-semibold text-primary hover:underline"
              >
                Sign in
              </Link>{" "}
              to watch games as they happen.
            </div>
          ) : live.data && live.data.items.length > 0 ? (
            <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
              {live.data.items.map((g) => (
                <Link
                  key={g.id}
                  to="/game/$gameId"
                  params={{ gameId: g.id }}
                  className="lift rounded-lg border border-border p-2 hover:border-primary/60"
                >
                  <MiniBoard fen={g.fen} />
                  <p className="mt-2 truncate text-xs font-semibold">
                    {name(g.whitePlayerId)} vs {name(g.blackPlayerId)}
                  </p>
                  <Pill className="mt-1.5">
                    {timeControlLabel(g.initialTimeSeconds, g.incrementSeconds)} {gameCategory(g)}
                  </Pill>
                </Link>
              ))}
            </div>
          ) : (
            <p className="mt-6 text-sm text-muted-foreground">No games in progress right now.</p>
          )}
        </Reveal>
      </section>
    </div>
  );
}
