import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { MailCheck } from "lucide-react";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { AuthLayout, FormError } from "@/components/auth/AuthLayout";
import { Field } from "@/components/auth/Field";
import { Button } from "@/components/ui/button";
import { authApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { validateEmail } from "@/lib/validation";

export const Route = createFileRoute("/verify-email")({
  validateSearch: (s: Record<string, unknown>) => ({
    email: typeof s["email"] === "string" ? s["email"] : undefined,
  }),
  head: () => ({ meta: [{ title: "Verify your email — Echelon Chess" }] }),
  component: VerifyEmail,
});

function VerifyEmail() {
  const search = Route.useSearch();
  const navigate = useNavigate();
  const [email, setEmail] = useState(search.email ?? "");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (cooldown <= 0) return;
    const id = window.setTimeout(() => setCooldown((c) => c - 1), 1000);
    return () => window.clearTimeout(id);
  }, [cooldown]);

  const verify = useCallback(async (e: string, verificationCode: string) => {
    setBusy(true);
    setError(null);
    try {
      await authApi.verifyEmail({ email: e.trim(), code: verificationCode.trim() });
      setDone(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }, []);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const emailErr = validateEmail(email);
    if (emailErr) return setError(emailErr);
    if (!/^\d{6}$/.test(code)) return setError("Enter the six-digit code from your email.");
    void verify(email, code);
  };

  const resend = async () => {
    const emailErr = validateEmail(email);
    if (emailErr) return setError(emailErr);
    try {
      await authApi.resendVerification(email.trim());
      toast.success("If that account needs verifying, a new code is on its way.");
      setCooldown(45);
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  if (done) {
    return (
      <AuthLayout title="Email verified" subtitle="You're all set. Sign in to start playing.">
        <div className="grid place-items-center gap-5 py-4 text-center">
          <MailCheck className="h-12 w-12 text-live" />
          <Button
            size="lg"
            className="w-full font-mono uppercase tracking-[0.14em] glow"
            onClick={() => void navigate({ to: "/login", search: { redirect: undefined } })}
          >
            Continue to sign in
          </Button>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title="Verify email"
      subtitle="Enter the six-digit code from your inbox. You can paste it directly into the field."
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
      <form onSubmit={submit} noValidate className="space-y-4">
        <Field
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="you@example.com"
        />
        <Field
          label="6-digit code"
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
          placeholder="000000"
          inputMode="numeric"
          maxLength={6}
          pattern="[0-9]{6}"
          autoComplete="one-time-code"
        />
        <FormError message={error} />
        <Button
          type="submit"
          size="lg"
          disabled={busy}
          className="w-full font-mono uppercase tracking-[0.14em] glow"
        >
          {busy ? "Verifying…" : "Verify email"}
        </Button>
        <Button
          type="button"
          variant="ghost"
          className="w-full"
          disabled={cooldown > 0}
          onClick={() => void resend()}
        >
          {cooldown > 0 ? `Resend code in ${cooldown}s` : "Resend code"}
        </Button>
      </form>
    </AuthLayout>
  );
}
