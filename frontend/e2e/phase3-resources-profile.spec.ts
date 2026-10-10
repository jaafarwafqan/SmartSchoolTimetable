import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatNumber } from "../src/lib/format";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { breakpoints, expectBreakpointScreenshots, expectNoPageScrollX, expectNoSeriousA11yViolations, expectNoTextOverlap, goToSection, setupOwner } from "./support/flows";

// Phase 3B screens on a fresh temporary database: «الموارد» (quick add, required by a subject, protected delete)
// and «ملف الجدولة» (weights, profile version, restore defaults).
const school = messages.school;
const resources = school.resources;
const profile = school.schedulingProfile;
const arab = (value: number) => formatNumber(value, "arab");

test("(3B) resources: quick add, a subject requires one, delete is protected; the profile saves and restores", async ({ browser, page }) => {
  const server = new ApiServer();
  await server.start(browser, "phase3-resources");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Resources-Owner-1");
    await prepareSchool(page, server.baseUrl, { schoolType: "intermediate", shiftMode: "morning", grades: [{ gradeKey: "intermediate-1", sections: 2 }] });
    await api(page, server.baseUrl, "POST", "/subjects/", {
      name: "التربية الرياضية", colorIndex: 0, priority: 0, distributionEnabled: true, spreadAcrossDays: false, heavy: false,
      requiresDoublePeriod: false, blockedPeriods: [], notes: null, requiredResourceId: null, version: 0,
    });

    // Quick add: name, kind and capacity in one row; Enter adds.
    await goToSection(page, school.nav.resources);
    const add = page.getByRole("form", { name: resources.add });
    await add.getByLabel(resources.name).fill("الساحة الرياضية");
    await add.getByLabel(resources.kind).selectOption({ label: resources.kinds.field });
    await add.getByLabel(resources.name).press("Enter");
    await expect(page.getByRole("status").filter({ hasText: resources.added("الساحة الرياضية") })).toBeVisible();
    await add.getByLabel(resources.name).fill("مختبر الحاسوب");
    await add.getByRole("button", { name: resources.increaseCapacity }).click();
    await add.getByRole("button", { name: resources.addButton }).click();
    await expect(page.getByText(resources.capacityValue(arab(2)))).toBeVisible();
    await expectNoSeriousA11yViolations(page, "resources");

    // The subject requires the sports field: its row says so.
    await goToSection(page, school.nav.subjects);
    await page.getByRole("button", { name: /التربية الرياضية/ }).first().click();
    await page.getByLabel(school.requiredResource.label).selectOption({ label: "الساحة الرياضية" });
    await page.getByRole("button", { name: school.subjects.save }).click();
    await expect(page.getByText(school.requiredResource.badge("الساحة الرياضية"))).toBeVisible();

    // Deleting the field is blocked: the dialog names the subject and keeps «حذف» disabled.
    await goToSection(page, school.nav.resources);
    await page.getByRole("button", { name: new RegExp(`^${school.common.delete}.*الساحة الرياضية`) }).click();
    const dialog = page.getByRole("dialog", { name: resources.deleteTitle });
    await expect(dialog.getByText(school.references.deleteBlocked)).toBeVisible();
    await expect(dialog.getByText(/مادة واحدة \(التربية الرياضية\)/)).toBeVisible();
    await expect(dialog.getByRole("button", { name: school.common.delete })).toBeDisabled();
    await dialog.getByRole("button", { name: messages.app.cancel }).click();

    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `resources at ${width}px`);
      await expectNoTextOverlap(page.locator("main"), `resources at ${width}px`);
    }
    await expectBreakpointScreenshots(page, "phase3-resources");
    await page.setViewportSize({ width: 1280, height: 900 });

    // Profile: change one weight (version 2), then restore the defaults (version 3).
    await goToSection(page, school.nav.schedulingProfile);
    await expect(page.getByText(profile.profileVersion(arab(1)))).toBeVisible();
    await page.getByLabel(profile.weightOf(profile.rules.avoidTeacherGaps)).selectOption(String(60));
    await page.getByRole("button", { name: profile.save }).click();
    await expect(page.getByRole("status").filter({ hasText: profile.saved })).toBeVisible();
    await expect(page.getByText(profile.profileVersion(arab(2)))).toBeVisible();
    await page.getByRole("button", { name: profile.restore }).click();
    await page.getByRole("dialog", { name: profile.restoreTitle }).getByRole("button", { name: profile.restore }).click();
    await expect(page.getByRole("status").filter({ hasText: profile.restored })).toBeVisible();
    await expect(page.getByText(profile.profileVersion(arab(3)))).toBeVisible();
    await expect(page.getByLabel(profile.weightOf(profile.rules.avoidTeacherGaps))).toHaveValue("30");
    await expectNoSeriousA11yViolations(page, "scheduling profile");
    for (const width of breakpoints) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `profile at ${width}px`);
      await expectNoTextOverlap(page.locator("main"), `profile at ${width}px`);
    }
    await expectBreakpointScreenshots(page, "phase3-profile");
  } finally {
    await server.stop();
  }
});
