import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { AnimatePresence, motion } from "motion/react";
import { Bell, Check, Swords, UserPlus, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { Avatar } from "@/components/common/bits";
import { Button } from "@/components/ui/button";
import { challengesApi, rematchApi, socialApi } from "@/lib/api/endpoints";
import { useInbox, useUsernames } from "@/lib/api/hooks";
import { errorMessage } from "@/lib/api/http";
import { timeControlLabel } from "@/lib/chess-utils";

/** Bell with everything addressed to me: challenges, rematch requests and friend requests. */
export function Notifications() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { challenges, rematches, friendRequests, count } = useInbox();
  const { name } = useUsernames([
    ...challenges.map((c) => c.challengerUserId),
    ...rematches.map((r) => r.requesterUserId),
  ]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["inbox"] });
  const fail = (e: unknown) => {
    toast.error(errorMessage(e));
    refresh();
  };

  const acceptChallenge = useMutation({
    mutationFn: (id: string) => challengesApi.accept(id),
    onSuccess: (r) => {
      refresh();
      setOpen(false);
      void navigate({ to: "/game/$gameId", params: { gameId: r.gameId } });
    },
    onError: fail,
  });
  const declineChallenge = useMutation({
    mutationFn: (id: string) => challengesApi.decline(id),
    onSuccess: refresh,
    onError: fail,
  });
  const acceptRematch = useMutation({
    mutationFn: (id: string) => rematchApi.accept(id),
    onSuccess: (r) => {
      refresh();
      setOpen(false);
      void navigate({ to: "/game/$gameId", params: { gameId: r.rematchGameId } });
    },
    onError: fail,
  });
  const declineRematch = useMutation({
    mutationFn: (id: string) => rematchApi.decline(id),
    onSuccess: refresh,
    onError: fail,
  });
  const acceptFriend = useMutation({
    mutationFn: (id: string) => socialApi.accept(id),
    onSuccess: () => {
      refresh();
      void queryClient.invalidateQueries({ queryKey: ["social"] });
      toast.success("Friend added");
    },
    onError: fail,
  });
  const declineFriend = useMutation({
    mutationFn: (id: string) => socialApi.decline(id),
    onSuccess: refresh,
    onError: fail,
  });

  const Row = ({
    icon,
    title,
    sub,
    onAccept,
    onDecline,
    busy,
  }: {
    icon: React.ReactNode;
    title: string;
    sub: string;
    onAccept: () => void;
    onDecline: () => void;
    busy: boolean;
  }) => (
    <li className="flex items-center gap-3 px-3 py-3">
      <span className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-secondary text-primary">
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{title}</p>
        <p className="label-mono mt-0.5 truncate">{sub}</p>
      </div>
      <Button
        size="icon"
        className="h-10 w-10 sm:h-8 sm:w-8"
        disabled={busy}
        onClick={onAccept}
        aria-label="Accept"
      >
        <Check className="h-4 w-4" />
      </Button>
      <Button
        size="icon"
        variant="secondary"
        className="h-10 w-10 sm:h-8 sm:w-8"
        disabled={busy}
        onClick={onDecline}
        aria-label="Decline"
      >
        <X className="h-4 w-4" />
      </Button>
    </li>
  );

  return (
    <div ref={ref} className="relative">
      <Button
        variant="ghost"
        size="icon"
        onClick={() => setOpen((o) => !o)}
        aria-label={`Notifications${count ? ` (${count})` : ""}`}
        aria-expanded={open}
      >
        <Bell className="h-4 w-4" />
        {count > 0 && (
          <span className="absolute right-1 top-1 grid h-4 min-w-4 place-items-center rounded-full bg-primary px-1 font-mono text-[9px] font-bold text-primary-foreground">
            {count}
          </span>
        )}
      </Button>
      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: -6, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -6 }}
            className="panel fixed inset-x-3 top-[3.75rem] z-[60] overflow-hidden sm:absolute sm:inset-x-auto sm:right-0 sm:top-full sm:mt-2 sm:w-[380px]"
          >
            <div className="border-b border-border px-3 py-2.5">
              <span className="label-mono">Notifications</span>
            </div>
            {count === 0 ? (
              <p className="px-4 py-8 text-center text-sm text-muted-foreground">
                You're all caught up.
              </p>
            ) : (
              <ul className="max-h-[60vh] divide-y divide-border overflow-y-auto">
                {challenges.map((c) => (
                  <Row
                    key={c.id}
                    icon={<Swords className="h-4 w-4" />}
                    title={`${name(c.challengerUserId)} challenges you`}
                    sub={`${timeControlLabel(c.initialTimeSeconds, c.incrementSeconds)} · ${c.rated ? "rated" : "casual"}`}
                    busy={acceptChallenge.isPending || declineChallenge.isPending}
                    onAccept={() => acceptChallenge.mutate(c.id)}
                    onDecline={() => declineChallenge.mutate(c.id)}
                  />
                ))}
                {rematches.map((r) => (
                  <Row
                    key={r.id}
                    icon={<Swords className="h-4 w-4" />}
                    title={`${name(r.requesterUserId)} wants a rematch`}
                    sub={`${timeControlLabel(r.initialTimeSeconds, r.incrementSeconds)} · ${r.rated ? "rated" : "casual"}`}
                    busy={acceptRematch.isPending || declineRematch.isPending}
                    onAccept={() => acceptRematch.mutate(r.id)}
                    onDecline={() => declineRematch.mutate(r.id)}
                  />
                ))}
                {friendRequests.map((f) => (
                  <li key={f.id} className="flex items-center gap-3 px-3 py-3">
                    <Avatar name={f.username} className="h-9 w-9" />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold">{f.username}</p>
                      <p className="label-mono mt-0.5 inline-flex items-center gap-1">
                        <UserPlus className="h-3 w-3" /> friend request
                      </p>
                    </div>
                    <Button
                      size="icon"
                      className="h-10 w-10 sm:h-8 sm:w-8"
                      disabled={acceptFriend.isPending}
                      onClick={() => acceptFriend.mutate(f.id)}
                      aria-label="Accept"
                    >
                      <Check className="h-4 w-4" />
                    </Button>
                    <Button
                      size="icon"
                      variant="secondary"
                      className="h-10 w-10 sm:h-8 sm:w-8"
                      disabled={declineFriend.isPending}
                      onClick={() => declineFriend.mutate(f.id)}
                      aria-label="Decline"
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
