import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { AuthLayout, FormError } from "@/components/auth/AuthLayout";
import { Field } from "@/components/auth/Field";
import { Button } from "@/components/ui/button";
import { authApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { validateEmail, validatePassword } from "@/lib/validation";

export const Route = createFileRoute("/reset-password")({
  validateSearch: (s: Record<string, unknown>) => ({
    email: typeof s["email"] === "string" ? s["email"] : undefined,
    token: typeof s["token"] === "string" ? s["token"] : undefined,
  }),
  head: () => ({ meta: [{ title: "Choose a new password — Echelon Chess" }] }),
  component: Reset,
});

function Reset() {
  const search = Route.useSearch();
  const navigate = useNavigate();
  const [email, setEmail] = useState(search.email ?? "");
  const [token, setToken] = useState(search.token ?? "");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const err =
      validateEmail(email) ??
      (token.trim() ? undefined : "The reset token is missing.") ??
      validatePassword(password) ??
      (password === confirm ? undefined : "Passwords don't match.");
    if (err) return setError(err);
    setBusy(true);
    setError(null);
    try {
      await authApi.resetPassword({
        email: email.trim(),
        token: token.trim(),
        newPassword: password,
      });
      toast.success("Password updated. Sign in with your new password.");
      void navigate({ to: "/login", search: { redirect: undefined } });
    } catch (e2) {
      setError(errorMessage(e2));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="New password"
      subtitle="Choose a strong password you haven't used before."
      footer={
        <Link to="/forgot-password" className="hover:text-foreground hover:underline">
          Need a new reset token?
        </Link>
      }
    >
      <form onSubmit={submit} noValidate className="space-y-4">
        {(!search.email || !search.token) && (
          <>
            <Field
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
            <Field
              label="Token"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              placeholder="Paste the token from your email"
            />
          </>
        )}
        <Field
          label="New password"
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          hint="At least 10 characters, with letters and numbers."
        />
        <Field
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
        />
        <FormError message={error} />
        <Button
          type="submit"
          size="lg"
          disabled={busy}
          className="w-full font-mono uppercase tracking-[0.14em] glow"
        >
          {busy ? "Saving…" : "Update password"}
        </Button>
      </form>
    </AuthLayout>
  );
}
