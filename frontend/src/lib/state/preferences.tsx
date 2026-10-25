import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

export interface Preferences {
  /** Tilted broadcast camera (true) or top-down (false). */
  perspective: boolean;
  sounds: boolean;
  /** 0–100 */
  volume: number;
  /** Require a second click on the target square before a move is played. */
  confirmMoves: boolean;
}

const DEFAULTS: Preferences = { perspective: true, sounds: true, volume: 70, confirmMoves: false };
const KEY = "echelon.preferences";

interface Ctx {
  prefs: Preferences;
  set: <K extends keyof Preferences>(key: K, value: Preferences[K]) => void;
}
const PreferencesContext = createContext<Ctx | null>(null);

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [prefs, setPrefs] = useState<Preferences>(DEFAULTS);

  // Read after mount so server and client render the same HTML.
  useEffect(() => {
    try {
      const raw = window.localStorage.getItem(KEY);
      if (raw) setPrefs({ ...DEFAULTS, ...(JSON.parse(raw) as Partial<Preferences>) });
    } catch {
      /* ignore corrupt storage */
    }
  }, []);

  const set = useCallback<Ctx["set"]>((key, value) => {
    setPrefs((p) => {
      const next = { ...p, [key]: value };
      try {
        window.localStorage.setItem(KEY, JSON.stringify(next));
      } catch {
        /* storage unavailable */
      }
      return next;
    });
  }, []);

  const value = useMemo(() => ({ prefs, set }), [prefs, set]);
  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences() {
  const ctx = useContext(PreferencesContext);
  if (!ctx) throw new Error("usePreferences must be used inside <PreferencesProvider>");
  return ctx;
}
