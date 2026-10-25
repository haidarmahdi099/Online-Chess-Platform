import { Link } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";
import { motion } from "motion/react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function Spinner({ className }: { className?: string }) {
  return (
    <Loader2
      className={cn("h-5 w-5 animate-spin text-muted-foreground", className)}
      aria-label="Loading"
    />
  );
}

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div className="min-w-0">
        <h1 className="font-display text-3xl font-bold uppercase sm:text-5xl">{title}</h1>
        {subtitle && <p className="mt-3 max-w-xl text-sm text-muted-foreground">{subtitle}</p>}
      </div>
      {actions}
    </div>
  );
}

export function EmptyState({
  title,
  text,
  action,
}: {
  title: string;
  text?: string;
  action?: ReactNode;
}) {
  return (
    <div className="panel grid place-items-center px-6 py-12 text-center">
      <p className="font-display text-lg font-bold uppercase">{title}</p>
      {text && <p className="mt-2 max-w-sm text-sm text-muted-foreground">{text}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="panel grid place-items-center px-6 py-12 text-center" role="alert">
      <p className="text-sm text-destructive">{message}</p>
      {onRetry && (
        <Button className="mt-4" variant="secondary" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}

/** Placeholder shaped like a card; shimmers while data loads. */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      aria-hidden
      className={cn("shimmer relative overflow-hidden rounded-lg bg-secondary/60", className)}
    />
  );
}

/**
 * Loading state: a knight hopping along a strip of squares, with skeleton rows under it so
 * the page keeps its shape. Used wherever a list or page waits on the server.
 */
export function LoadingBlock({ className }: { className?: string }) {
  return (
    <div
      className={cn("grid place-items-center gap-6 py-14", className)}
      role="status"
      aria-label="Loading"
    >
      <div className="relative flex h-10 w-40 overflow-hidden rounded-md border border-border">
        {[0, 1, 2, 3].map((i) => (
          <span
            key={i}
            className={cn("h-full w-1/4", i % 2 ? "bg-board-dark" : "bg-board-light")}
          />
        ))}
        <motion.span
          aria-hidden
          className="absolute left-0 top-0 grid h-10 w-1/4 place-items-center text-2xl leading-none text-primary"
          animate={{
            x: ["0%", "100%", "200%", "300%", "200%", "100%", "0%"],
            y: [0, -8, 0, -8, 0, -8, 0],
          }}
          transition={{ duration: 2.4, repeat: Infinity, ease: "easeInOut" }}
        >
          ♞
        </motion.span>
      </div>
      <div className="w-full max-w-md space-y-2.5 px-2">
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-5/6" />
      </div>
    </div>
  );
}

export function SignInPrompt({ text, redirect }: { text: string; redirect: string }) {
  return (
    <EmptyState
      title="Sign in required"
      text={text}
      action={
        <Button asChild>
          <Link to="/login" search={{ redirect }}>
            Sign in
          </Link>
        </Button>
      }
    />
  );
}

export function Pill({
  children,
  tone = "neutral",
  className,
}: {
  children: ReactNode;
  tone?: "neutral" | "good" | "bad" | "primary" | "info";
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-sm px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-[0.12em]",
        tone === "neutral" && "bg-secondary text-muted-foreground",
        tone === "good" && "bg-live/20 text-live",
        tone === "bad" && "bg-destructive/20 text-destructive",
        tone === "primary" && "bg-primary/20 text-primary",
        tone === "info" && "bg-accent/20 text-accent",
        className,
      )}
    >
      {children}
    </span>
  );
}

export function Pager({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (p: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <div className="flex items-center justify-between gap-3 pt-4">
      <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Previous
      </Button>
      <span className="label-mono">
        Page {page} of {pages}
      </span>
      <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Next
      </Button>
    </div>
  );
}

/** Two-letter initials avatar (the backend stores no avatars). */
export function Avatar({ name, className }: { name: string; className?: string }) {
  const hue = [...name].reduce((h, c) => (h * 31 + c.charCodeAt(0)) % 360, 7);
  return (
    <span
      aria-hidden
      className={cn(
        "grid h-9 w-9 shrink-0 place-items-center rounded-md font-mono text-xs font-bold text-white",
        className,
      )}
      style={{ background: `oklch(0.55 0.14 ${hue})` }}
    >
      {name.slice(0, 2).toUpperCase()}
    </span>
  );
}
