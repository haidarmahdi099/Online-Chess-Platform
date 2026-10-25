/** Tiny WebAudio move sounds (no audio files needed). */
export type SoundKind = "move" | "capture" | "check" | "end";

let ctx: AudioContext | null = null;

const TONES: Record<SoundKind, { freq: number; ms: number; type: OscillatorType }> = {
  move: { freq: 330, ms: 70, type: "triangle" },
  capture: { freq: 220, ms: 110, type: "square" },
  check: { freq: 520, ms: 160, type: "sawtooth" },
  end: { freq: 180, ms: 320, type: "triangle" },
};

/** volume: 0–100 */
export function playSound(kind: SoundKind, volume: number) {
  if (typeof window === "undefined" || volume <= 0) return;
  try {
    ctx ??= new AudioContext();
    if (ctx.state === "suspended") void ctx.resume();
    const { freq, ms, type } = TONES[kind];
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    const t = ctx.currentTime;
    osc.type = type;
    osc.frequency.value = freq;
    gain.gain.setValueAtTime((volume / 100) * 0.12, t);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + ms / 1000);
    osc.connect(gain).connect(ctx.destination);
    osc.start(t);
    osc.stop(t + ms / 1000);
  } catch {
    /* audio blocked or unsupported: ignore */
  }
}
