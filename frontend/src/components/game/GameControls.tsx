import { Boxes, Flag, Handshake, RefreshCw, XCircle } from "lucide-react";
import { Button } from "@/components/ui/button";

interface GameControlsProps {
  onFlip: () => void;
  onTogglePerspective: () => void;
  perspective: boolean;
  /** Player actions (hidden/disabled for spectators and finished games). */
  canAct: boolean;
  canAbort: boolean;
  drawPending: boolean;
  onOfferDraw: () => void;
  onResign: () => void;
  onAbort: () => void;
}

export function GameControls({
  onFlip,
  onTogglePerspective,
  perspective,
  canAct,
  canAbort,
  drawPending,
  onOfferDraw,
  onResign,
  onAbort,
}: GameControlsProps) {
  const items = [
    { icon: RefreshCw, label: "Flip", onClick: onFlip, enabled: true, danger: false },
    {
      icon: Boxes,
      label: perspective ? "2D" : "3D",
      onClick: onTogglePerspective,
      enabled: true,
      danger: false,
    },
    {
      icon: Handshake,
      label: drawPending ? "Offered" : "Draw",
      onClick: onOfferDraw,
      enabled: canAct && !drawPending && !canAbort,
      danger: false,
    },
    canAbort
      ? { icon: XCircle, label: "Abort", onClick: onAbort, enabled: canAct, danger: true }
      : { icon: Flag, label: "Resign", onClick: onResign, enabled: canAct, danger: true },
  ];
  return (
    <div className="grid grid-cols-4 gap-1.5">
      {items.map(({ icon: Icon, label, onClick, danger, enabled }) => (
        <Button
          key={label}
          variant={danger ? "destructive" : "secondary"}
          size="sm"
          onClick={onClick}
          disabled={!enabled}
          className="flex h-auto min-h-14 flex-col gap-1 py-2 font-mono text-[10px] uppercase tracking-[0.1em] active:scale-[0.97] sm:min-h-0"
        >
          <Icon className="h-4 w-4" />
          {label}
        </Button>
      ))}
    </div>
  );
}
