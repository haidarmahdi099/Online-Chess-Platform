/** Small form validators (no schema library needed for two forms). */
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
const USERNAME = /^[A-Za-z0-9_]{3,20}$/;

export const validateEmail = (v: string) =>
  EMAIL.test(v.trim()) ? undefined : "Enter a valid email address.";

export const validateUsername = (v: string) =>
  USERNAME.test(v.trim()) ? undefined : "3–20 characters: letters, numbers and underscores.";

export const validatePassword = (v: string) => {
  if (v.length < 10) return "Use at least 10 characters.";
  if (!/[A-Za-z]/.test(v) || !/\d/.test(v)) return "Mix letters and numbers.";
  return undefined;
};

/** 0–4 rough strength score for the meter. */
export function passwordStrength(v: string): number {
  let score = 0;
  if (v.length >= 10) score++;
  if (v.length >= 12) score++;
  if (/[A-Z]/.test(v) && /[a-z]/.test(v)) score++;
  if (/\d/.test(v) && /[^A-Za-z0-9]/.test(v)) score++;
  return score;
}
