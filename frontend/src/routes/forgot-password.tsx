import { Link, createFileRoute } from "@tanstack/react-router";
import { MailCheck } from "lucide-react";
import { useState, type FormEvent } from "react";
import { AuthLayout, FormError } from "@/components/auth/AuthLayout";
import { Field } from "@/components/auth/Field";
import { Button } from "@/components/ui/button";
import { authApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { validateEmail } from "@/lib/validation";

export const Route = createFileRoute("/forgot-password")({
  head: () => ({ meta: [{ title: "Reset password — Echelon Chess" }] }),
  component: Forgot,
});

function Forgot() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const err = validateEmail(email);
    if (err) return setError(err);
    setBusy(true);
    setError(null);
    try {
      await authApi.forgotPassword(email.trim());
      setSent(true);
    } catch (e2) {
      setError(errorMessage(e2));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Forgot password"
      subtitle="Enter your email and we'll send you instructions to reset your password."
      footer={
        <Link
          to="/login"
          search={{ redirect: undefined }}
          className="hover:text-foreground hover:underline"
        >
          Back to sign in
        </Link>
      }
    >
      {sent ? (
        <div className="grid place-items-center gap-3 py-4 text-center">
          <MailCheck className="h-12 w-12 text-live" />
          <p className="text-sm text-muted-foreground">
            If an account exists for <strong className="text-foreground">{email}</strong>, reset
            instructions are on the way. It may take a minute.
          </p>
        </div>
      ) : (
        <form onSubmit={submit} noValidate className="space-y-4">
          <Field
            label="Email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@example.com"
          />
          <FormError message={error} />
          <Button
            type="submit"
            size="lg"
            disabled={busy}
            className="w-full font-mono uppercase tracking-[0.14em] glow"
          >
            {busy ? "Sending…" : "Send reset instructions"}
          </Button>
        </form>
      )}
    </AuthLayout>
  );
}
