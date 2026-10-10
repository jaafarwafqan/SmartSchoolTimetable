import { describe, expect, it } from "vitest";
import { weightChoices } from "./profileApi";

describe("weightChoices", () => {
  it("offers 0 to 100 in steps of five", () => {
    const choices = weightChoices(30);
    expect(choices).toHaveLength(21);
    expect([choices[0], choices[1], choices[20]]).toEqual([0, 5, 100]);
  });

  it("keeps a saved value that is not a multiple of five, in order", () => {
    const choices = weightChoices(42);
    expect(choices).toHaveLength(22);
    expect(choices.slice(8, 11)).toEqual([40, 42, 45]);
  });
});
