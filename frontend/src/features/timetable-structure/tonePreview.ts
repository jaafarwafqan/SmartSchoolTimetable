import type { BellTone } from "./scheduleApi";

/** Note frequencies (Hz) of each built-in tone; 0 is a rest. Values match the API tone names (ADR 0018). */
export const tonePatterns: Record<BellTone, readonly number[]> = {
  classic: [660, 660],
  chime: [523, 659, 784],
  beeps: [880, 0, 880],
  soft: [440],
};

const noteSeconds = 0.16;

/** Synthesizes a short preview locally with the Web Audio API; no audio files and no ringing schedule. */
export async function previewTone(tone: BellTone, createContext: () => AudioContext = () => new window.AudioContext()): Promise<void> {
  const context = createContext();
  const now = context.currentTime;
  tonePatterns[tone].forEach((frequency, index) => {
    if (frequency === 0) return;
    const start = now + index * noteSeconds;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(0.12, start + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.13);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(start);
    oscillator.stop(start + 0.14);
  });
  await new Promise<void>((resolve) => window.setTimeout(resolve, tonePatterns[tone].length * noteSeconds * 1000 + 100));
  await context.close();
}
