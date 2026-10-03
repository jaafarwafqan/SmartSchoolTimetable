import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PasswordField } from "./PasswordField";

describe("PasswordField", () => {
  it("toggles visibility with a labelled, pressed-state eye control", () => {
    render(
      <PasswordField
        id="password"
        label="كلمة المرور"
        autoComplete="current-password"
      />,
    );
    const input = screen.getByLabelText("كلمة المرور") as HTMLInputElement;
    const toggle = screen.getByRole("button", { name: "إظهار كلمة المرور" });

    expect(input.type).toBe("password");
    expect(toggle).toHaveAttribute("aria-pressed", "false");
    expect(toggle).toHaveAttribute("title", "إظهار كلمة المرور");

    fireEvent.click(toggle);
    expect(input.type).toBe("text");
    expect(screen.getByRole("button", { name: "إخفاء كلمة المرور" })).toHaveAttribute("aria-pressed", "true");
  });
});
