import type { Locator } from "@playwright/test";

/**
 * UX metric (Phase 2.5 report): how much of a setup scenario is typed versus chosen. `type` = a value written into a
 * text field; `choose` = a selection (card, checkbox, select, stepper, chip); `act` = a command (next, preview,
 * apply, finish), counted separately because it enters no data.
 */
export class UxMeter {
  typed = 0;
  chosen = 0;
  commands = 0;

  constructor(readonly scenario: string) {}

  async type(locator: Locator, value: string): Promise<void> {
    this.typed++;
    await locator.fill(value);
  }

  async choose(locator: Locator): Promise<void> {
    this.chosen++;
    await locator.click();
  }

  async check(locator: Locator, checked = true): Promise<void> {
    this.chosen++;
    await locator.setChecked(checked);
  }

  async select(locator: Locator, option: string | { label: string }): Promise<void> {
    this.chosen++;
    await locator.selectOption(option);
  }

  async act(locator: Locator): Promise<void> {
    this.commands++;
    await locator.click();
  }

  /** Printed for the report: share of data entries that were selections. */
  report(): string {
    const entries = this.typed + this.chosen;
    const share = entries === 0 ? 0 : Math.round((this.chosen / entries) * 100);
    const line = `UX ${this.scenario}: typed=${this.typed} chosen=${this.chosen} commands=${this.commands} selected-share=${share}%`;
    console.log(line);
    return line;
  }
}
