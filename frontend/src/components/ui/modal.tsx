import { AnimatePresence, motion } from "motion/react";
import { X } from "lucide-react";
import { useEffect, type ReactNode } from "react";
import { cn } from "@/lib/utils";

interface ModalProps {
  open: boolean;
  onClose?: () => void;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
  /** Hide the close button and ignore Escape/backdrop clicks (for required decisions). */
  persistent?: boolean;
}

/** Accessible, animated modal. No portal needed: it is fixed-position above everything. */
export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  className,
  persistent,
}: ModalProps) {
  useEffect(() => {
    if (!open || persistent || !onClose) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, persistent, onClose]);

  // Stop the page behind a dialog from scrolling (matters most on phones).
  useEffect(() => {
    if (!open) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prev;
    };
  }, [open]);

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[100] grid place-items-end overflow-y-auto bg-background/70 backdrop-blur-sm sm:place-items-center sm:p-4"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onMouseDown={(e) => {
            if (e.target === e.currentTarget && !persistent) onClose?.();
          }}
        >
          <motion.div
            role="dialog"
            aria-modal="true"
            aria-label={title}
            initial={{ opacity: 0, y: 40, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 24, scale: 0.98 }}
            transition={{ type: "spring", stiffness: 380, damping: 32 }}
            // Phones get a bottom sheet (reachable with a thumb); larger screens a centred card.
            className={cn(
              "panel pb-safe relative max-h-[92dvh] w-full max-w-none overflow-y-auto rounded-b-none p-5 sm:max-w-md sm:rounded-b-xl sm:p-6",
              className,
            )}
          >
            {!persistent && onClose && (
              <button
                type="button"
                onClick={onClose}
                aria-label="Close"
                className="absolute right-2 top-2 grid h-11 w-11 place-items-center sm:right-3 sm:top-3 sm:h-8 sm:w-8 rounded-md text-muted-foreground hover:bg-secondary hover:text-foreground"
              >
                <X className="h-4 w-4" />
              </button>
            )}
            <h2 className="pr-8 font-display text-xl font-bold uppercase">{title}</h2>
            {description && <p className="mt-2 text-sm text-muted-foreground">{description}</p>}
            <div className="mt-5">{children}</div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
