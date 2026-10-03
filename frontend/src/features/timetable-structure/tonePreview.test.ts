import { afterEach, describe, expect, it, vi } from "vitest";
import { previewTone } from "./tonePreview";

describe("bell tone preview", () => {
  afterEach(() => vi.restoreAllMocks());

  it("synthesizes a short preview and closes the audio context", async () => {
    const oscillator = { type: "", frequency: { value: 0 }, connect: vi.fn(), start: vi.fn(), stop: vi.fn() };
    oscillator.connect.mockReturnValue({ connect: vi.fn() });
    const gain = { gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
    const context = {
      currentTime: 1,
      destination: {},
      createOscillator: vi.fn(() => oscillator),
      createGain: vi.fn(() => gain),
      close: vi.fn(async () => undefined),
    };
    class MockAudioContext {
      currentTime = context.currentTime;
      destination = context.destination;
      createOscillator = context.createOscillator;
      createGain = context.createGain;
      close = context.close;
    }
    vi.stubGlobal("AudioContext", MockAudioContext);
    vi.spyOn(window, "setTimeout").mockImplementation(((callback: TimerHandler) => {
      if (typeof callback === "function") callback();
      return 1;
    }) as typeof window.setTimeout);

    await previewTone("Chime");

    expect(context.createOscillator).toHaveBeenCalledTimes(3);
    expect(oscillator.start).toHaveBeenCalledTimes(3);
    expect(context.close).toHaveBeenCalledOnce();
  });
});
