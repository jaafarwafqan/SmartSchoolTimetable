import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { formatNumber } from "../src/lib/format";
import { ApiServer } from "./support/apiServer";
import { expectNoLatinText, expectNoSeriousA11yViolations } from "./support/flows";

// Phase 2.5D: the setup wizard on a temporary database. Scenario (a) of spec 2.5 §7: a morning-only intermediate
// school with three grades, set up through all seven steps, resumed after leaving, re-applied without changes.
const server = new ApiServer();
const school = messages.school;
const wizard = school.wizard;
const templates = school.templates;
const number = (value: number) => formatNumber(value); // a new school shows Arabic-Indic digits
const password = "Wizard-Owner-1";

test.describe.configure({ mode: "serial" });

test.beforeAll(async ({ browser }) => {
  await server.start(browser, "phase25-wizard");
});

test.afterAll(async () => {
  await server.stop();
});

test("a fresh school is set up through the wizard, resumed and finished", async ({ page }) => {
  await page.goto(server.baseUrl);
  await page.getByLabel(messages.app.username).fill("owner");
  await page.getByLabel(messages.app.password, { exact: true }).fill(password);
  await page.getByLabel(messages.app.confirmPassword).fill(password);
  await page.getByRole("button", { name: messages.app.createAccount }).click();
  await page.getByLabel(messages.app.confirmCodeSaved).check();
  await page.getByRole("button", { name: messages.app.continue }).click();

  // After the recovery code the wizard opens at step 1.
  await expect(page.getByRole("heading", { name: wizard.title, level: 1 })).toBeVisible();
  const stepTitle = (step: 1 | 2 | 3 | 4 | 5 | 6 | 7) => page.getByRole("heading", { name: wizard.steps[step], level: 2 });
  const next = () => page.getByRole("button", { name: wizard.next }).click();
  await expect(stepTitle(1)).toBeVisible();
  await next();
  await expect(page.getByText(messages.errors.REQUIRED).first()).toBeVisible(); // name is required
  await page.getByLabel(wizard.school.name).fill("متوسطة الفرات");
  await page.getByRole("radio", { name: new RegExp(`^${school.profile.schoolTypes.intermediate}`) }).check();
  await expect(page.getByRole("radio", { name: new RegExp(wizard.school.modes.morning) })).toBeChecked();
  await expectNoSeriousA11yViolations(page, "wizard step 1");
  await expectNoLatinText(page, "wizard step 1", ["owner"]);
  await next();

  // Step 2: the proposed year and terms are accepted as they are.
  await expect(stepTitle(2)).toBeFocused();
  await expect(page.getByText(wizard.year.terms)).toBeVisible();
  await expectNoSeriousA11yViolations(page, "wizard step 2");
  await next();

  // Step 3: Sunday–Thursday and the first period preset: 7 lessons a day = 35 a week, with a live preview.
  await expect(stepTitle(3)).toBeVisible();
  await expect(page.getByText(wizard.timing.weekly(number(35)))).toBeVisible();
  await expect(page.getByRole("table", { name: wizard.timing.previewTitle(wizard.timing.shifts.morning) }).getByRole("row")).toHaveCount(9); // header + 7 lessons + 1 break
  await page.getByText(wizard.timing.perDay).click();
  await page.getByRole("button", { name: school.scheduleStructure.decreaseFor(school.scheduleStructure.days.thursday) }).click();
  await expect(page.getByText(wizard.timing.weekly(number(34)))).toBeVisible();
  await expectNoSeriousA11yViolations(page, "wizard step 3");
  await expectNoLatinText(page, "wizard step 3", ["owner"]);
  await next();

  // Leave and come back: the dashboard offers «استكمال الإعداد» and the wizard resumes at step 4.
  await expect(stepTitle(4)).toBeVisible();
  await page.getByRole("link", { name: wizard.later }).click();
  await expect(page.getByRole("heading", { name: school.nav.dashboard, level: 1 })).toBeVisible();
  await page.getByRole("link", { name: wizard.open }).click();
  await expect(stepTitle(4)).toBeVisible();
  await expect(page.getByRole("navigation", { name: wizard.progressLabel }).locator("[aria-current=step]")).toContainText(wizard.steps[4]);

  // Step 4: the intermediate template (three grades, two sections each), applied once; a second preview changes nothing.
  await page.getByRole("button", { name: templates.preview }).click();
  await expect(page.locator(".plan-line")).toHaveCount(3);
  await page.getByRole("button", { name: templates.apply }).click();
  await expect(page.getByRole("status").filter({ hasText: templates.applied(number(3)) })).toBeVisible();
  await expect(page.locator(".stage-card")).toHaveCount(3);
  await page.getByRole("button", { name: templates.preview }).click();
  await expect(page.getByText(templates.noChanges)).toBeVisible();
  await expectNoSeriousA11yViolations(page, "wizard step 4");
  await next();

  // Step 5: suggested subjects, then one curriculum cell.
  await expect(stepTitle(5)).toBeVisible();
  await page.getByRole("button", { name: templates.preview }).click();
  await page.getByRole("button", { name: templates.apply }).click();
  await expect(page.locator(".curriculum-input").first()).toBeVisible();
  const cell = page.locator(".curriculum-input").first();
  await cell.fill("5");
  await cell.press("Enter");
  await expect(page.getByRole("status").filter({ hasText: school.curriculum.saved })).toBeVisible();
  await expect(page.locator(".curriculum-total").first()).toContainText(school.curriculum.status.under(number(29)));
  await expectNoSeriousA11yViolations(page, "wizard step 5");
  await next();

  // Step 6 is optional.
  await expect(stepTitle(6)).toBeVisible();
  await page.getByRole("button", { name: wizard.skip }).click();

  // Step 7: real counts and warnings, then finish.
  await expect(stepTitle(7)).toBeVisible();
  await expect(page.locator(".count-item", { hasText: wizard.review.stages })).toContainText(number(3));
  await expect(page.locator(".count-item", { hasText: wizard.review.sections })).toContainText(number(6));
  await expect(page.locator(".review-warnings > li")).not.toHaveCount(0);
  await expectNoSeriousA11yViolations(page, "wizard step 7");
  await expectNoLatinText(page, "wizard review", ["owner"]);
  await page.getByRole("button", { name: wizard.finish }).click();

  await expect(page.getByRole("heading", { name: school.nav.dashboard, level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: wizard.open })).toHaveCount(0);
  await expect(page.locator(".curriculum-status-row")).toHaveCount(3);
  await expect(page.locator(".curriculum-status-row").first()).toContainText(school.curriculum.status.under(number(29)));
  await expectNoSeriousA11yViolations(page, "dashboard after the wizard");

  // Settings keeps a way back into the wizard.
  await page.goto(`${server.baseUrl}/settings`);
  await page.getByRole("link", { name: school.nav.setupWizard }).click();
  await expect(page.getByRole("heading", { name: wizard.title, level: 1 })).toBeVisible();
});
