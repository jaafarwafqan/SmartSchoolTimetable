import "@testing-library/jest-dom/vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Lock, LogOut, Settings } from "lucide-react";
import { describe, expect, it, vi } from "vitest";
import { Menu } from "./menu";

function renderMenu() {
  const onLock = vi.fn();
  render(
    <Menu
      label="قائمة المستخدم"
      trigger={<span>{"owner"}</span>}
      items={[
        { key: "settings", label: "الإعدادات", icon: <Settings aria-hidden="true" />, onSelect: vi.fn() },
        { key: "lock", label: "قفل الشاشة", icon: <Lock aria-hidden="true" />, onSelect: onLock },
        { key: "logout", label: "تسجيل الخروج", icon: <LogOut aria-hidden="true" />, onSelect: vi.fn() },
      ]}
    />,
  );
  return { onLock, trigger: screen.getByRole("button", { name: "قائمة المستخدم" }) };
}

describe("Menu", () => {
  it("opens from the keyboard, moves with arrow keys and closes with Escape", () => {
    const { trigger } = renderMenu();
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    fireEvent.keyDown(trigger, { key: "ArrowDown" });
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    const items = screen.getAllByRole("menuitem");
    expect(items[0]).toHaveFocus();

    fireEvent.keyDown(items[0], { key: "ArrowDown" });
    expect(items[1]).toHaveFocus();
    fireEvent.keyDown(items[1], { key: "ArrowUp" });
    fireEvent.keyDown(items[0], { key: "ArrowUp" });
    expect(items[2]).toHaveFocus();
    fireEvent.keyDown(items[2], { key: "Home" });
    expect(items[0]).toHaveFocus();

    fireEvent.keyDown(items[0], { key: "Escape" });
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it("runs the chosen item and closes", () => {
    const { onLock, trigger } = renderMenu();
    fireEvent.click(trigger);
    fireEvent.click(screen.getByRole("menuitem", { name: "قفل الشاشة" }));
    expect(onLock).toHaveBeenCalledOnce();
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });
});
