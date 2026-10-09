import { describe, expect, it } from "vitest";
import { clockOf, formatTime12, parseTime24, toTime24 } from "./time";

describe("12-hour time (Iraqi convention)", () => {
  it.each([
    ["00:00", "١٢:٠٠ ص", "12:00 ص"],
    ["00:05", "١٢:٠٥ ص", "12:05 ص"],
    ["11:59", "١١:٥٩ ص", "11:59 ص"],
    ["12:00", "١٢:٠٠ م", "12:00 م"],
    ["12:30", "١٢:٣٠ م", "12:30 م"],
    ["13:00", "١:٠٠ م", "1:00 م"],
    ["23:59", "١١:٥٩ م", "11:59 م"],
    ["08:05", "٨:٠٥ ص", "8:05 ص"],
  ])("%s → %s", (value, arabic, western) => {
    expect(formatTime12(value)).toBe(arabic);
    expect(formatTime12(value, "latn")).toBe(western);
  });

  it("round-trips between 24-hour storage and the 12-hour parts", () => {
    for (let minutes = 0; minutes < 1440; minutes += 1) {
      const value = clockOf(minutes);
      const parts = parseTime24(value);
      expect(parts).not.toBeNull();
      expect(toTime24(parts!)).toBe(value);
    }
    expect(toTime24({ hour: 12, minute: 0, meridiem: "am" })).toBe("00:00");
    expect(toTime24({ hour: 12, minute: 0, meridiem: "pm" })).toBe("12:00");
  });

  it("never shows a 24-hour clock and leaves invalid text alone", () => {
    for (let minutes = 0; minutes < 1440; minutes += 7)
      expect(formatTime12(clockOf(minutes), "latn")).not.toMatch(/\b(1[3-9]|2[0-3]):\d\d\b/);
    expect(parseTime24("24:00")).toBeNull();
    expect(parseTime24("")).toBeNull();
    expect(formatTime12("bad")).toBe("bad");
  });
});
