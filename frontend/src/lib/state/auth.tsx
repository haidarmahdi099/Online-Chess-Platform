import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "@tanstack/react-router";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import { authApi, usersApi } from "@/lib/api/endpoints";
import { ApiError } from "@/lib/api/http";
import { tokenStore } from "@/lib/api/tokens";
import type { CurrentUser, RegisterResult } from "@/lib/api/types";
import { hub, type HubStatus } from "@/lib/realtime/hub";

type AuthStatus = "loading" | "authenticated" | "anonymous";

interface AuthContextValue {
  user: CurrentUser | null;
  status: AuthStatus;
  isAdmin: boolean;
  realtimeStatus: HubStatus;
  login: (usernameOrEmail: string, password: string) => Promise<void>;
  register: (body: {
    username: string;
    email: string;
    password: string;
  }) => Promise<RegisterResult>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");
  const realtimeStatus = useSyncExternalStore(
    hub.subscribeStatus,
    hub.getStatus,
    () => "disconnected" as HubStatus,
  );
  const statusRef = useRef(status);
  statusRef.current = status;

  const loadMe = useCallback(async () => {
    try {
      const me = await usersApi.me();
      setUser(me);
      setStatus("authenticated");
    } catch (e) {
      // Offline at startup keeps the tokens so a reload can recover; anything else ends the session.
      if (!(e instanceof ApiError) || e.status !== 0) tokenStore.set(null);
      setUser(null);
      setStatus("anonymous");
    }
  }, []);

  useEffect(() => {
    if (tokenStore.get()) void loadMe();
    else setStatus("anonymous");
  }, [loadMe]);

  // Sign-in/out in another tab, or a refresh token that stopped working.
  useEffect(
    () =>
      tokenStore.subscribe((session) => {
        if (!session && statusRef.current === "authenticated") {
          setUser(null);
          setStatus("anonymous");
          queryClient.clear();
        } else if (session && statusRef.current === "anonymous") {
          void loadMe();
        }
      }),
    [loadMe, queryClient],
  );

  // Realtime follows the session.
  useEffect(() => {
    if (status === "authenticated") void hub.start();
    else if (status === "anonymous") void hub.stop();
  }, [status]);

  const login = useCallback(
    async (usernameOrEmail: string, password: string) => {
      const res = await authApi.login({ usernameOrEmail: usernameOrEmail.trim(), password });
      tokenStore.set({
        accessToken: res.accessToken,
        refreshToken: res.refreshToken,
        userId: res.userId,
        username: res.username,
      });
      queryClient.clear();
      await loadMe();
    },
    [loadMe, queryClient],
  );

  const register = useCallback(
    (body: { username: string; email: string; password: string }) => authApi.register(body),
    [],
  );

  const logout = useCallback(async () => {
    const refresh = tokenStore.get()?.refreshToken;
    if (refresh) await authApi.logout(refresh).catch(() => undefined);
    tokenStore.set(null);
    await hub.stop();
    queryClient.clear();
    setUser(null);
    setStatus("anonymous");
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      status,
      isAdmin: user?.role === "Admin",
      realtimeStatus,
      login,
      register,
      logout,
      refreshUser: loadMe,
    }),
    [user, status, realtimeStatus, login, register, logout, loadMe],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}

/** Only same-site paths are allowed as a post-login destination. */
export function safeRedirect(value: unknown): string | undefined {
  return typeof value === "string" && value.startsWith("/") && !value.startsWith("//")
    ? value
    : undefined;
}

/** For pages that need an account: sends anonymous visitors to /login and back again. */
export function useRequireAuth() {
  const auth = useAuth();
  const router = useRouter();
  useEffect(() => {
    if (auth.status === "anonymous") {
      void router.navigate({ to: "/login", search: { redirect: router.state.location.pathname } });
    }
  }, [auth.status, router]);
  return auth;
}
