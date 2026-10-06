import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { arabicCount } from "../src/lib/arabicCount";
import { formatNumber } from "../src/lib/format";
import { ApiServer } from "./support/apiServer";
import {
  expectBreakpointScreenshots,
  expectNoLatinText,
  expectNoPageScrollX,
  expectNoSeriousA11yViolations,
  expectYearInOrder,
  goToSection,
} from "./support/flows";
import { UxMeter } from "./support/ux";

// Phase 2.5E scenario (b): a dual-shift secondary school (ثانوية) with both preparatory branches, set up through the
// wizard. Also (c) the template re-run changes nothing, (d) year order, (e) the phone menu is no drawer, screenshots
// of every wizard step and the curriculum at four widths, and the typed-versus-chosen UX metric.
const server = new ApiServer();
const school = messages.school;
const wizard = school.wizard;
const templates = school.templates;
const curriculum = school.curriculum;
const arab = (value: number) => formatNumber(value, "arab");
const password = "Scenario-Owner-1";

test.describe.configure({ mode: "serial" });
test.beforeAll(async ({ browser }) => { await server.start(browser, "phase25-scenarios"); });
test.afterAll(async () => { await server.stop(); });

const stepTitle = (page: Page, step: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8) => page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[step] });

test("scenario (b): a dual-shift ثانوية with branches through the wizard", async ({ page }) => {
  // A fixed date keeps the proposed year (and the screenshots) stable.
  await page.clock.setFixedTime(new Date("2026-10-05T09:00:00+03:00"));
  await page.goto(server.baseUrl);
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(password);
  await page.getByLabel(messages.app.confirmPassword).fill(password);
  await page.getByRole("button", { name: messages.app.createAccount }).click();
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();
  const ux = new UxMeter("(b) dual-shift secondary with branches"); // counts the wizard only, not the account
  const next = () => ux.act(page.getByRole("button", { name: wizard.next }));

  // 1. School: name typed; type and shift mode chosen on cards.
  await expect(stepTitle(page, 1)).toBeVisible();
  await ux.type(page.getByLabel(wizard.school.name), "ثانوية دجلة للبنين");
  await ux.check(page.getByRole("radio", { name: new RegExp(`^${school.profile.schoolTypes.secondary}`) }));
  await ux.check(page.getByRole("radio", { name: new RegExp(`^${wizard.school.modes.dual}`) }));
  await expectBreakpointScreenshots(page, "wizard-step-1");
  await next();

  // 2. Year: the proposal is accepted.
  await expect(stepTitle(page, 2)).toBeVisible();
  await expectBreakpointScreenshots(page, "wizard-step-2");
  await next();

  // 3. Timing: two shift blocks; the evening takes the evening preset and teaches one lesson less on Thursday.
  await expect(stepTitle(page, 3)).toBeVisible();
  const blocks = page.locator(".wizard-shift");
  await expect(blocks).toHaveCount(2);
  await ux.select(page.locator("#wizard-evening-preset"), "evening-single-break");
  const evening = blocks.nth(1);
  await ux.choose(evening.getByText(wizard.timing.perDay));
  await ux.choose(evening.getByRole("button", { name: school.scheduleStructure.decreaseFor(school.scheduleStructure.days.thursday) }));
  await expect(evening.getByText(wizard.timing.weekly(arab(29)))).toBeVisible();
  await expect(blocks.nth(0).getByText(wizard.timing.weekly(arab(35)))).toBeVisible();
  await expectNoSeriousA11yViolations(page, "scenario b step 3");
  await expectBreakpointScreenshots(page, "wizard-step-3");
  await next();

  // 4. Stages: all six grades, both branches; the preparatory grades study in the evening; one section each.
  await expect(stepTitle(page, 4)).toBeVisible();
  for (const grade of ["الرابع الإعدادي", "الخامس الإعدادي", "السادس الإعدادي"])
    await ux.select(page.getByLabel(templates.gradeShift(grade)), { label: "الدوام المسائي" });
  await ux.choose(page.getByRole("button", { name: templates.sectionsDecrease }));
  await ux.act(page.getByRole("button", { name: templates.preview }));
  await expect(page.locator(".plan-line")).toHaveCount(9);
  await ux.act(page.getByRole("button", { name: templates.apply }));
  await expect(page.getByRole("status").filter({ hasText: templates.applied(arab(9)) })).toBeVisible();
  // (c) applying the template again would change nothing.
  await ux.act(page.getByRole("button", { name: templates.preview }));
  await expect(page.getByText(templates.noChanges)).toBeVisible();
  await expect(page.locator(".stage-card")).toHaveCount(9);
  await expect(page.locator(".stage-card", { hasText: "الأول المتوسط" }).locator(".stage-card-capacity")).toContainText(arabicCount(35, "lesson", arab));
  await expect(page.locator(".stage-card", { hasText: "الرابع العلمي" }).locator(".stage-card-capacity")).toContainText(arabicCount(29, "lesson", arab));
  await expectBreakpointScreenshots(page, "wizard-step-4");
  await next();

  // 5. Subjects and curriculum: the suggestions become chips, one cell is typed.
  await expect(stepTitle(page, 5)).toBeVisible();
  await ux.act(page.getByRole("button", { name: templates.preview }));
  await ux.act(page.getByRole("button", { name: templates.apply }));
  await expect(page.locator(".subject-chips .subject-chip").first()).toBeVisible();
  const firstCell = page.locator(".curriculum-input").first();
  await ux.type(firstCell, "4");
  await firstCell.press("Enter");
  await expect(page.getByRole("status").filter({ hasText: curriculum.saved })).toBeVisible();
  await expect(page.locator(".curriculum-table tfoot td").first()).toContainText(curriculum.status.under(arab(31)));
  await expect(page.locator(".curriculum-table thead th.curriculum-stage-head").nth(3)).toContainText(curriculum.headerCapacity(arab(29)));
  await expectNoSeriousA11yViolations(page, "scenario b step 5");
  await expectBreakpointScreenshots(page, "wizard-step-5");
  await next();

  // 6. Teachers and 7. workload are optional (the workload step came with Phase 3E); 8. review, then finish.
  await expect(stepTitle(page, 6)).toBeVisible();
  await expectBreakpointScreenshots(page, "wizard-step-6");
  await ux.act(page.getByRole("button", { name: wizard.skip }));
  await expect(stepTitle(page, 7)).toBeVisible();
  await expectNoSeriousA11yViolations(page, "scenario b step 7");
  await expectBreakpointScreenshots(page, "wizard-step-7");
  await ux.act(page.getByRole("button", { name: wizard.skip }));
  await expect(stepTitle(page, 8)).toBeVisible();
  await expect(page.locator(".count-item", { hasText: wizard.review.shifts })).toContainText(arab(2));
  await expect(page.locator(".count-item", { hasText: wizard.review.sections })).toContainText(arab(9));
  await expectNoLatinText(page, "scenario b review", ["owner"]);
  await expectBreakpointScreenshots(page, "wizard-step-8");
  await ux.act(page.getByRole("button", { name: wizard.finish }));
  await expect(page.getByRole("heading", { name: school.nav.dashboard, level: 1 })).toBeVisible();
  ux.report();

  // The curriculum screen at four widths.
  await goToSection(page, school.nav.curriculum);
  await expect(page.locator(".curriculum-table")).toBeVisible();
  await expectBreakpointScreenshots(page, "curriculum");

  // (d) The year label keeps its order in RTL.
  await goToSection(page, school.nav.academicYears);
  await expectYearInOrder(page.getByRole("table", { name: school.nav.academicYears }), "2026", "2027");

  // (e) No drawers: on a phone the menu opens in the page flow, never as a dialog or fixed panel.
  await page.setViewportSize({ width: 375, height: 800 });
  await page.getByRole("button", { name: school.nav.openMenu }).click();
  const menu = page.locator(".app-mobile-menu");
  await expect(menu).toBeVisible();
  expect(await menu.evaluate((element) => getComputedStyle(element).position)).toBe("static");
  await expect(page.locator("dialog[open]")).toHaveCount(0);
  await expectNoPageScrollX(page, "mobile menu");
});
