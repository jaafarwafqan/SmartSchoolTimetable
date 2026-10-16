import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, setupOwner } from "./support/flows";
import { seedReadySchool } from "./support/readySchool";

// M4b: the screens the owner found hard on a phone, at 375 px (the file name is a leftover of its first life as a probe).
const server = new ApiServer();

test("(M4b) 375 px: no sideways page scroll, tabs in one row, tables as cards, a short term badge", async ({ browser, page }) => {
  test.setTimeout(240_000);
  await server.start(browser, "m4b-mobile");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Mobile-Owner-1");
    await seedReadySchool(page, server.baseUrl);
    await page.reload();
    await page.setViewportSize({ width: 375, height: 800 });

    // The top bar says «الفصل الأول» once, never «الفصل: الفصل الأول».
    await page.goto(`${server.baseUrl}/`);
    const term = page.locator(".topbar-context .ui-badge").last();
    await expect(term).toBeVisible();
    await expect(term).not.toContainText(`${messages.school.shell.currentTerm("x").split(":")[0]}: `);

    for (const path of ["/", "/settings/general", "/settings/account", "/settings/backup", "/settings/advanced", "/about", "/teachers/workload", "/teachers/list", "/school/year", "/classes/stages"]) {
      await page.goto(`${server.baseUrl}${path}`);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
      await expectNoPageScrollX(page, path);
    }

    // Settings tabs: one row that scrolls sideways (no wrapped stack).
    await page.goto(`${server.baseUrl}/settings/backup`);
    const tabs = page.locator(".ui-tabs ul");
    // Count only once the tabs are on screen (CI measured an empty list right after navigation).
    await expect(tabs.locator("li").first()).toBeVisible();
    const tops = await tabs.locator("li").evaluateAll((items) => new Set(items.map((item) => Math.round(item.getBoundingClientRect().top))).size);
    expect(tops).toBe(1);

    // The backup list and the workload table are cards on a phone: their cells carry the column header as a label.
    await expect(page.locator(".ui-table-stack").first()).toBeVisible();
    await page.goto(`${server.baseUrl}/teachers/workload`);
    const cell = page.locator(".workload-table td").first();
    await expect(cell).toBeVisible();
    expect(await cell.evaluate((element) => getComputedStyle(element).display)).toBe("grid");
    await expectNoTextOverlap(page.locator(".workload-matrix"), "workload at 375");
    await expectNoSeriousA11yViolations(page, "workload at 375");
  } finally {
    await server.stop();
  }
});
