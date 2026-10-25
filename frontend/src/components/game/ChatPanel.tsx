import { Send } from "lucide-react";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ChatMessage } from "@/lib/api/types";
import { useUsernames } from "@/lib/api/hooks";

interface ChatPanelProps {
  messages: ChatMessage[];
  myId: string | null;
  canSend: boolean;
  onSend: (message: string) => Promise<void> | void;
}

export function ChatPanel({ messages, myId, canSend, onSend }: ChatPanelProps) {
  const { name } = useUsernames(messages.map((m) => m.userId));
  const [draft, setDraft] = useState("");
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [messages.length]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const body = draft.trim();
    if (!body) return;
    setDraft("");
    void onSend(body);
  };

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-b border-border px-3 py-2">
        <span className="label-mono">Game chat</span>
      </div>
      <div className="min-h-0 flex-1 space-y-2 overflow-y-auto px-3 py-3">
        {messages.length === 0 && (
          <p className="py-6 text-center text-xs text-muted-foreground">
            {canSend ? "Say hello to your opponent." : "Chat is only visible to the two players."}
          </p>
        )}
        {messages.map((m) => (
          <p key={m.id} className="break-words text-[13px] leading-snug">
            <span
              className={
                m.userId === myId ? "font-semibold text-primary" : "font-semibold text-accent"
              }
            >
              {m.userId === myId ? "You" : name(m.userId)}
            </span>
            <span className="text-muted-foreground"> · </span>
            <span>{m.message}</span>
          </p>
        ))}
        <div ref={endRef} />
      </div>
      <form onSubmit={submit} className="flex gap-2 border-t border-border p-2">
        <Input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder={canSend ? "Say something…" : "Chat is for players"}
          disabled={!canSend}
          maxLength={300}
          className="h-9 text-sm"
        />
        <Button
          type="submit"
          size="icon"
          className="h-9 w-9 shrink-0"
          disabled={!canSend}
          aria-label="Send"
        >
          <Send className="h-4 w-4" />
        </Button>
      </form>
    </div>
  );
}
