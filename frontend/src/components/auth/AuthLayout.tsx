import { Link } from "@tanstack/react-router";
import { motion } from "motion/react";
import { Puzzle, Radio, Trophy } from "lucide-react";
import type { ReactNode } from "react";
import heroBoard from "@/assets/hero-board.jpg";

const PERKS = [
  { icon: Radio, title: "Live broadcasts", text: "Watch games the moment they happen." },
  {
    icon: Trophy,
    title: "Rated play",
    text: "Climb the leaderboard in bullet, blitz, rapid and classical.",
  },
  {
    icon: Puzzle,
    title: "Daily puzzles",
    text: "Sharpen your tactics and track your puzzle rating.",
  },
];

/** Shared two-column frame for every account page. */
export function AuthLayout({
  title,
  subtitle,
  children,
  footer,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <div className="relative isolate overflow-hidden hero-bg">
      <div className="pointer-events-none absolute inset-0 -z-10">
        <img src={heroBoard} alt="" className="h-full w-full object-cover opacity-35" />
        <div className="absolute inset-0 bg-gradient-to-b from-background/80 via-background/70 to-background" />
      </div>

      <div className="mx-auto grid min-h-[calc(100dvh-130px)] max-w-[1400px] items-center gap-12 px-4 py-12 sm:px-6 lg:grid-cols-[1.05fr_0.95fr] lg:py-16">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.55 }}
          className="hidden lg:block"
        >
          <Link to="/" className="inline-flex items-center gap-2">
            <span className="grid h-9 w-9 place-items-center rounded-md bg-primary text-xl text-primary-foreground">
              ♞
            </span>
            <span className="font-display text-xl font-bold tracking-[0.18em]">ECHELON</span>
          </Link>
          <h2 className="mt-10 font-display text-[clamp(2.2rem,4.4vw,3.8rem)] font-bold uppercase leading-[0.95]">
            Your next move
            <br />
            starts here
          </h2>
          <p className="mt-5 max-w-md font-mono text-sm leading-relaxed text-muted-foreground">
            One account for rated games, live broadcasts, tournaments and training.
          </p>
          <ul className="mt-10 space-y-4">
            {PERKS.map(({ icon: Icon, title: t, text }) => (
              <li key={t} className="flex items-start gap-4">
                <span className="mt-0.5 grid h-10 w-10 shrink-0 place-items-center rounded-lg border border-border bg-surface/70 text-primary">
                  <Icon className="h-5 w-5" />
                </span>
                <div>
                  <p className="text-sm font-semibold">{t}</p>
                  <p className="text-sm text-muted-foreground">{text}</p>
                </div>
              </li>
            ))}
          </ul>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 24 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.55, delay: 0.1 }}
          className="panel mx-auto w-full max-w-md p-6 sm:p-8"
        >
          <p className="label-mono">Echelon account</p>
          <h1 className="mt-2 font-display text-3xl font-bold uppercase">{title}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{subtitle}</p>
          <div className="mt-6">{children}</div>
          {footer && (
            <div className="mt-6 border-t border-border pt-5 text-center text-sm text-muted-foreground">
              {footer}
            </div>
          )}
        </motion.div>
      </div>
    </div>
  );
}

export function FormError({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <p
      role="alert"
      className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
    >
      {message}
    </p>
  );
}
