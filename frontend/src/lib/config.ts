/** Gateway base URL. Override with VITE_API_BASE_URL (no trailing slash). */
const configuredBaseUrl = import.meta.env["VITE_API_BASE_URL"] as string | undefined;

// Development defaults to the local Gateway. Production defaults to same-origin routing so a
// static frontend can use `/api` and `/hubs/game` behind the deployment's reverse proxy.
export const API_BASE_URL = (
  configuredBaseUrl ?? (import.meta.env.DEV ? "http://localhost:5272" : "")
)
  .trim()
  .replace(/\/+$/, "");

export const API_URL = `${API_BASE_URL}/api`;
export const HUB_URL = `${API_BASE_URL}/hubs/game`;
