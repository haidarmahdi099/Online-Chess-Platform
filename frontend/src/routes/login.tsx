import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { Loader2 } from "lucide-react";
import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { AuthLayout, FormError } from "@/components/auth/AuthLayout";
import { Field } from "@/components/auth/Field";
import { Button } from "@/components/ui/button";
import { ApiError, errorMessage } from "@/lib/api/http";
import { safeRedirect, useAuth } from "@/lib/state/auth";

export const Route = createFileRoute("/login")({
  validateSearch: (search: Record<string, unknown>) => ({
    redirect: safeRedirect(search["redirect"]),
  }),
  head: () => ({ meta: [{ title: "Sign in — Echelon Chess" }] }),
  component: Login,
});

function Login() {
  const { redirect } = Route.useSearch();
  const navigate = useNavigate();
  const { login, status } = useAuth();
  const [id, setId] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<{ id?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (status === "authenticated") void navigate({ to: redirect ?? "/", replace: true });
  }, [status, redirect, navigate]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const next = {
      id: id.trim() ? undefined : "Enter your username or email.",
      password: password ? undefined : "Enter your password.",
    };
    setErrors(next);
    setFormError(null);
    if (next.id || next.password) return;

    setBusy(true);
    try {
      await login(id, password);
      toast.success("Welcome back");
    } catch (err) {
      if (err instanceof ApiError && err.code === "email_not_verified") {
        toast.info("Verify your email to continue.");
        void navigate({
          to: "/verify-email",
          search: { email: id.includes("@") ? id.trim() : undefined },
        });
        return;
      }
      setFormError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Sign in"
      subtitle="Pick up where you left off."
      footer={
        <>
          New to Echelon?{" "}
          <Link to="/register" className="font-semibold text-primary hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={submit} noValidate className="space-y-4">
        <Field
          label="Username or email"
          autoComplete="username"
          value={id}
          onChange={(e) => setId(e.target.value)}
          error={errors.id}
          placeholder="magnus_c or you@example.com"
        />
        <Field
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={errors.password}
          placeholder="••••••••••"
        />
        <div className="-mt-1 text-right">
          <Link
            to="/forgot-password"
            className="text-xs text-muted-foreground hover:text-foreground hover:underline"
          >
            Forgot password?
          </Link>
        </div>
        <FormError message={formError} />
        <Button
          type="submit"
          size="lg"
          disabled={busy}
          className="w-full font-mono uppercase tracking-[0.14em] glow"
        >
          {busy ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" /> Signing in…
            </>
          ) : (
            "Sign in"
          )}
        </Button>
      </form>
      <p className="mt-4 text-center text-xs text-muted-foreground">
        <Link
          to="/leaderboard"
          search={{ category: "chess" }}
          className="hover:text-foreground hover:underline"
        >
          Browse the leaderboard as a guest
        </Link>
      </p>
    </AuthLayout>
  );
}
