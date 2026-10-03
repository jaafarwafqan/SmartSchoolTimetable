export type BellTone = "Classic" | "Chime" | "Beeps" | "Soft";

const patterns: Record<BellTone, readonly number[]> = {
  Classic: [660, 660],
  Chime: [523, 659, 784],
  Beeps: [880, 0, 880],
  Soft: [440],
};

/** Synthesizes a short preview locally; it does not request audio assets or start a ringing schedule. */
export async function previewTone(tone: BellTone): Promise<void> {
  const context = new window.AudioContext();
  const now = context.currentTime;
  patterns[tone].forEach((frequency, index) => {
    if (frequency === 0) return;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.0001, now + index * 0.16);
    gain.gain.exponentialRampToValueAtTime(0.12, now + index * 0.16 + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + index * 0.16 + 0.13);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(now + index * 0.16);
    oscillator.stop(now + index * 0.16 + 0.14);
  });
  await new Promise<void>((resolve) => window.setTimeout(resolve, patterns[tone].length * 160 + 100));
  await context.close();
}
