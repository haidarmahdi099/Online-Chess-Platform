import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";
import { useEffect, useState, type FormEvent } from "react";
import { AuthLayout, FormError } from "@/components/auth/AuthLayout";
import { Field } from "@/components/auth/Field";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/api/http";
import { useAuth } from "@/lib/state/auth";
import {
  passwordStrength,
  validateEmail,
  validatePassword,
  validateUsername,
} from "@/lib/validation";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/register")({
  head: () => ({ meta: [{ title: "Create account — Echelon Chess" }] }),
  component: Register,
});

const STRENGTH = ["Too weak", "Weak", "Okay", "Good", "Strong"];

function Register() {
  const navigate = useNavigate();
  const { register, status } = useAuth();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<Record<string, string | undefined>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const strength = passwordStrength(password);

  useEffect(() => {
    if (status === "authenticated") void navigate({ to: "/", replace: true });
  }, [status, navigate]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const next = {
      username: validateUsername(username),
      email: validateEmail(email),
      password: validatePassword(password),
      confirm: confirm === password ? undefined : "Passwords don't match.",
    };
    setErrors(next);
    setFormError(null);
    if (Object.values(next).some(Boolean)) return;

    setBusy(true);
    try {
      const res = await register({ username: username.trim(), email: email.trim(), password });
      void navigate({ to: "/verify-email", search: { email: res.email } });
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Create account"
      subtitle="Free forever. We'll email you a six-digit code to verify your address."
      footer={
        <>
          Already have an account?{" "}
          <Link
            to="/login"
            search={{ redirect: undefined }}
            className="font-semibold text-primary hover:underline"
          >
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={submit} noValidate className="space-y-4">
        <Field
          label="Username"
          autoComplete="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          error={errors["username"]}
          hint="3–20 characters: letters, numbers, underscores."
        />
        <Field
          label="Email"
          type="email"
          autoComplete="email"
          inputMode="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          error={errors["email"]}
          placeholder="you@example.com"
        />
        <div>
          <Field
            label="Password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors["password"]}
            hint="At least 10 characters, with letters and numbers."
          />
          {password && (
            <div className="mt-2 flex items-center gap-2" aria-live="polite">
              <div className="flex flex-1 gap-1">
                {[0, 1, 2, 3].map((i) => (
                  <span
                    key={i}
                    className={cn(
                      "h-1 flex-1 rounded-full bg-border transition-colors",
                      i < strength &&
                        (strength <= 1
                          ? "bg-destructive"
                          : strength === 2
                            ? "bg-chart-5"
                            : "bg-live"),
                    )}
                  />
                ))}
              </div>
              <span className="label-mono">{STRENGTH[strength]}</span>
            </div>
          )}
        </div>
        <Field
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          error={errors["confirm"]}
        />
        <FormError message={formError} />
        <Button
          type="submit"
          size="lg"
          disabled={busy}
          className="w-full font-mono uppercase tracking-[0.14em] glow"
        >
          {busy ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Creating…
            </>
          ) : (
            "Create account"
          )}
        </Button>
      </form>
    </AuthLayout>
  );
}
