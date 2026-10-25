import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { Switch } from "@/components/ui/switch";
import { challengesApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { categoryFor, timeControlLabel } from "@/lib/chess-utils";
import { cn } from "@/lib/utils";

const PRESETS = [
  { initial: 60, inc: 0 },
  { initial: 180, inc: 0 },
  { initial: 300, inc: 3 },
  { initial: 600, inc: 0 },
  { initial: 900, inc: 10 },
  { initial: 1800, inc: 0 },
];

interface Props {
  open: boolean;
  onClose: () => void;
  opponentId: string;
  opponentName: string;
}

/** Sends a direct challenge (POST /challenges). The opponent is notified in their inbox. */
export function ChallengeModal({ open, onClose, opponentId, opponentName }: Props) {
  const [idx, setIdx] = useState(2);
  const [rated, setRated] = useState(false);
  const queryClient = useQueryClient();
  const preset = PRESETS[idx]!;

  const send = useMutation({
    mutationFn: () =>
      challengesApi.create({
        opponentUserId: opponentId,
        initialTimeSeconds: preset.initial,
        incrementSeconds: preset.inc,
        rated,
      }),
    onSuccess: () => {
      toast.success(`Challenge sent to ${opponentName}`);
      void queryClient.invalidateQueries({ queryKey: ["challenges"] });
      onClose();
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Challenge ${opponentName}`}
      description="They'll get a notification and have 24 hours to accept."
    >
      <div className="grid grid-cols-3 gap-2">
        {PRESETS.map((p, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setIdx(i)}
            className={cn(
              "rounded-md border px-3 py-3 text-center transition-colors",
              i === idx
                ? "border-primary bg-primary/10"
                : "border-border bg-secondary hover:border-primary/50",
            )}
          >
            <span className="block font-mono text-lg font-bold">
              {timeControlLabel(p.initial, p.inc)}
            </span>
            <span className="label-mono">{categoryFor(p.initial, p.inc)}</span>
          </button>
        ))}
      </div>
      <div className="mt-4 flex items-center gap-3">
        <Switch checked={rated} onCheckedChange={setRated} id="challenge-rated" />
        <label htmlFor="challenge-rated" className="label-mono">
          {rated ? "Rated" : "Casual"}
        </label>
      </div>
      <div className="mt-6 flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>
        <Button onClick={() => send.mutate()} disabled={send.isPending}>
          {send.isPending ? "Sending…" : "Send challenge"}
        </Button>
      </div>
    </Modal>
  );
}
