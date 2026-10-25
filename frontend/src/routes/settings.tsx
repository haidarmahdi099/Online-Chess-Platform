import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { Field } from "@/components/auth/Field";
import { FormError } from "@/components/auth/AuthLayout";
import { LoadingBlock, PageHeader } from "@/components/common/bits";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { Slider } from "@/components/ui/slider";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { authApi, socialApi, usersApi } from "@/lib/api/endpoints";
import { errorMessage } from "@/lib/api/http";
import { tokenStore } from "@/lib/api/tokens";
import type { Preferences } from "@/lib/api/types";
import { playSound } from "@/lib/sound";
import { useAuth, useRequireAuth } from "@/lib/state/auth";
import { usePreferences } from "@/lib/state/preferences";
import { validateEmail, validatePassword, validateUsername } from "@/lib/validation";

export const Route = createFileRoute("/settings")({
  head: () => ({ meta: [{ title: "Settings — Echelon Chess" }] }),
  component: Settings,
});

function Row({
  title,
  text,
  children,
}: {
  title: string;
  text?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-6 py-4">
      <div className="min-w-0">
        <p className="text-sm font-semibold">{title}</p>
        {text && <p className="mt-1 text-xs text-muted-foreground">{text}</p>}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}

function Settings() {
  const { status } = useRequireAuth();
  if (status !== "authenticated") return <LoadingBlock className="py-32" />;
  return (
    <div className="mx-auto max-w-[800px] px-4 py-10 sm:px-6 sm:py-16">
      <PageHeader
        title="Settings"
        subtitle="Tune the board, control who can reach you and manage your account."
      />
      <Tabs defaultValue="play" className="mt-8">
        <TabsList className="grid w-full grid-cols-3 font-mono text-[10px] uppercase tracking-[0.04em] sm:text-xs sm:tracking-[0.1em]">
          <TabsTrigger value="play">Play</TabsTrigger>
          <TabsTrigger value="account">Account</TabsTrigger>
          <TabsTrigger value="security">Security</TabsTrigger>
        </TabsList>
        <TabsContent value="play" className="mt-5">
          <PlayTab />
        </TabsContent>
        <TabsContent value="account" className="mt-5">
          <AccountTab />
        </TabsContent>
        <TabsContent value="security" className="mt-5">
          <SecurityTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function PlayTab() {
  const { prefs, set } = usePreferences();
  const queryClient = useQueryClient();
  const remote = useQuery({ queryKey: ["social", "preferences"], queryFn: socialApi.preferences });
  const save = useMutation({
    mutationFn: (next: Omit<Preferences, "userId" | "updatedAt">) =>
      socialApi.savePreferences(next),
    onSuccess: (p) => queryClient.setQueryData(["social", "preferences"], p),
    onError: (e) => {
      toast.error(errorMessage(e));
      void queryClient.invalidateQueries({ queryKey: ["social", "preferences"] });
    },
  });

  const update = (patch: Partial<Preferences>) => {
    if (!remote.data) return;
    const { userId: _u, updatedAt: _t, ...rest } = remote.data;
    const next = { ...rest, ...patch };
    queryClient.setQueryData(["social", "preferences"], { ...remote.data, ...patch });
    save.mutate(next);
  };

  // The server-side sound flag mirrors the local one so both stay consistent across devices.
  useEffect(() => {
    if (remote.data && remote.data.soundEnabled !== prefs.sounds)
      set("sounds", remote.data.soundEnabled);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remote.data?.soundEnabled]);

  return (
    <div className="panel divide-y divide-border px-5">
      <Row title="3D board camera" text="Tilted broadcast view, or a flat top-down view.">
        <Switch checked={prefs.perspective} onCheckedChange={(v) => set("perspective", v)} />
      </Row>
      <Row title="Sound effects" text="Moves, captures, checks and game end.">
        <Switch
          checked={prefs.sounds}
          onCheckedChange={(v) => {
            set("sounds", v);
            update({ soundEnabled: v });
          }}
        />
      </Row>
      <Row title="Volume">
        <div className="w-40">
          <Slider
            value={[prefs.volume]}
            max={100}
            step={5}
            onValueChange={([v]) => set("volume", v ?? 70)}
            onValueCommit={() => prefs.sounds && playSound("move", prefs.volume)}
            disabled={!prefs.sounds}
          />
        </div>
      </Row>
      {remote.isLoading ? (
        <LoadingBlock className="py-8" />
      ) : remote.data ? (
        <>
          <Row title="Allow challenges" text="Let other players send you direct challenges.">
            <Switch
              checked={remote.data.allowChallenges}
              onCheckedChange={(v) => update({ allowChallenges: v })}
            />
          </Row>
          <Row title="Show online status" text="Let friends see when you are online.">
            <Switch
              checked={remote.data.showOnlineStatus}
              onCheckedChange={(v) => update({ showOnlineStatus: v })}
            />
          </Row>
          <Row title="Premoves" text="Stored with your account for when premoves are enabled.">
            <Switch
              checked={remote.data.premovesEnabled}
              onCheckedChange={(v) => update({ premovesEnabled: v })}
            />
          </Row>
        </>
      ) : (
        <p className="py-6 text-sm text-muted-foreground">
          Account preferences couldn't be loaded.
        </p>
      )}
    </div>
  );
}

function AccountTab() {
  const { user, refreshUser } = useAuth();
  const queryClient = useQueryClient();

  const [username, setUsername] = useState(user?.username ?? "");
  const [uErr, setUErr] = useState<string | null>(null);
  const rename = useMutation({
    mutationFn: () => usersApi.updateProfile(username.trim()),
    onSuccess: async (p) => {
      const s = tokenStore.get();
      if (s) tokenStore.set({ ...s, username: p.username });
      await refreshUser();
      void queryClient.invalidateQueries();
      toast.success("Username updated");
    },
    onError: (e) => setUErr(errorMessage(e)),
  });

  const [newEmail, setNewEmail] = useState("");
  const [emailPwd, setEmailPwd] = useState("");
  const [emailToken, setEmailToken] = useState("");
  const [emailStep, setEmailStep] = useState<"form" | "token">("form");
  const [eErr, setEErr] = useState<string | null>(null);
  const requestEmail = useMutation({
    mutationFn: () => authApi.changeEmail({ newEmail: newEmail.trim(), password: emailPwd }),
    onSuccess: () => {
      setEmailStep("token");
      toast.success("Check your new inbox for a confirmation token.");
    },
    onError: (e) => setEErr(errorMessage(e)),
  });
  const confirmEmail = useMutation({
    mutationFn: () => authApi.confirmEmailChange(emailToken.trim()),
    onSuccess: async () => {
      await refreshUser();
      setEmailStep("form");
      setNewEmail("");
      setEmailPwd("");
      setEmailToken("");
      toast.success("Email updated");
    },
    onError: (e) => setEErr(errorMessage(e)),
  });

  const [cur, setCur] = useState("");
  const [next, setNext] = useState("");
  const [pErr, setPErr] = useState<string | null>(null);
  const changePwd = useMutation({
    mutationFn: () => authApi.changePassword({ currentPassword: cur, newPassword: next }),
    onSuccess: () => {
      setCur("");
      setNext("");
      toast.success("Password changed");
    },
    onError: (e) => setPErr(errorMessage(e)),
  });

  return (
    <div className="space-y-5">
      <form
        className="panel space-y-4 p-5"
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          const err = validateUsername(username);
          setUErr(err ?? null);
          if (!err && username.trim() !== user?.username) rename.mutate();
        }}
      >
        <h2 className="font-display text-base font-bold uppercase">Username</h2>
        <Field
          label="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          error={uErr ?? undefined}
        />
        <Button type="submit" disabled={rename.isPending || username.trim() === user?.username}>
          Save username
        </Button>
      </form>

      <form
        className="panel space-y-4 p-5"
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          setEErr(null);
          if (emailStep === "token") return confirmEmail.mutate();
          const err =
            validateEmail(newEmail) ?? (emailPwd ? undefined : "Enter your password to confirm.");
          if (err) return setEErr(err);
          requestEmail.mutate();
        }}
      >
        <h2 className="font-display text-base font-bold uppercase">Email</h2>
        <p className="text-sm text-muted-foreground">
          Current address: <strong className="text-foreground">{user?.email}</strong>
        </p>
        {emailStep === "form" ? (
          <>
            <Field
              label="New email"
              type="email"
              value={newEmail}
              onChange={(e) => setNewEmail(e.target.value)}
            />
            <Field
              label="Current password"
              type="password"
              value={emailPwd}
              onChange={(e) => setEmailPwd(e.target.value)}
            />
          </>
        ) : (
          <Field
            label="Confirmation token"
            value={emailToken}
            onChange={(e) => setEmailToken(e.target.value)}
            hint={`Sent to ${newEmail}`}
          />
        )}
        <FormError message={eErr} />
        <Button type="submit" disabled={requestEmail.isPending || confirmEmail.isPending}>
          {emailStep === "form" ? "Send confirmation" : "Confirm new email"}
        </Button>
      </form>

      <form
        className="panel space-y-4 p-5"
        onSubmit={(e: FormEvent) => {
          e.preventDefault();
          setPErr(null);
          const err = cur ? validatePassword(next) : "Enter your current password.";
          if (err) return setPErr(err);
          changePwd.mutate();
        }}
      >
        <h2 className="font-display text-base font-bold uppercase">Password</h2>
        <Field
          label="Current password"
          type="password"
          autoComplete="current-password"
          value={cur}
          onChange={(e) => setCur(e.target.value)}
        />
        <Field
          label="New password"
          type="password"
          autoComplete="new-password"
          value={next}
          onChange={(e) => setNext(e.target.value)}
          hint="At least 10 characters, with letters and numbers."
        />
        <FormError message={pErr} />
        <Button type="submit" disabled={changePwd.isPending}>
          Change password
        </Button>
      </form>
    </div>
  );
}

function SecurityTab() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);

  const everywhere = useMutation({
    mutationFn: () => authApi.logoutEverywhere(),
    onSuccess: async () => {
      toast.success("Signed out of every device");
      await logout();
      void navigate({ to: "/login", search: { redirect: undefined } });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });
  const del = useMutation({
    mutationFn: () => usersApi.deleteAccount(password),
    onSuccess: async () => {
      toast.success("Your account was deleted");
      await logout();
      void navigate({ to: "/" });
    },
    onError: (e) => setErr(errorMessage(e)),
  });

  return (
    <div className="space-y-5">
      <div className="panel p-5">
        <h2 className="font-display text-base font-bold uppercase">Sessions</h2>
        <p className="mt-2 text-sm text-muted-foreground">
          Sign out of this device and every other one where you're logged in.
        </p>
        <Button
          className="mt-4"
          variant="secondary"
          disabled={everywhere.isPending}
          onClick={() => everywhere.mutate()}
        >
          Sign out everywhere
        </Button>
      </div>
      <div className="panel border-destructive/40 p-5">
        <h2 className="font-display text-base font-bold uppercase text-destructive">
          Delete account
        </h2>
        <p className="mt-2 text-sm text-muted-foreground">
          Permanently removes your account. This can't be undone.
        </p>
        <Button
          className="mt-4"
          variant="destructive"
          onClick={() => {
            setErr(null);
            setPassword("");
            setOpen(true);
          }}
        >
          Delete my account
        </Button>
      </div>
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Delete your account?"
        description="Enter your password to confirm."
      >
        <form
          onSubmit={(e: FormEvent) => {
            e.preventDefault();
            if (!password) return setErr("Enter your password.");
            del.mutate();
          }}
          className="space-y-4"
        >
          <Field
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <FormError message={err} />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" variant="destructive" disabled={del.isPending}>
              Delete forever
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
