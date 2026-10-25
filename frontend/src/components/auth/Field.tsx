import { Eye, EyeOff } from "lucide-react";
import { useId, useState, type ComponentProps } from "react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

interface FieldProps extends Omit<ComponentProps<"input">, "id"> {
  label: string;
  error?: string | undefined;
  hint?: string;
}

/** Labelled input with inline error text and a show/hide toggle for passwords. */
export function Field({ label, error, hint, type = "text", className, ...props }: FieldProps) {
  const id = useId();
  const [shown, setShown] = useState(false);
  const isPassword = type === "password";

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="label-mono block">
        {label}
      </label>
      <div className="relative">
        <Input
          id={id}
          type={isPassword && shown ? "text" : type}
          aria-invalid={!!error}
          aria-describedby={error || hint ? `${id}-msg` : undefined}
          className={cn(
            "h-11 bg-surface/60 text-sm",
            isPassword && "pr-11",
            error && "border-destructive focus-visible:ring-destructive",
            className,
          )}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            onClick={() => setShown((s) => !s)}
            aria-label={shown ? "Hide password" : "Show password"}
            className="absolute inset-y-0 right-0 grid w-11 place-items-center text-muted-foreground hover:text-foreground"
          >
            {shown ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        )}
      </div>
      {(error || hint) && (
        <p
          id={`${id}-msg`}
          className={cn("text-xs", error ? "text-destructive" : "text-muted-foreground")}
        >
          {error ?? hint}
        </p>
      )}
    </div>
  );
}
