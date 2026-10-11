import { expect, test, type Page } from "@playwright/test";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { api } from "../e2e/support/api";
import { writeWorkbook } from "../e2e/support/xlsx";
import { logoPng, shot } from "./helpers";
import { app, baseUrl, data, fixedTime, school, signIn } from "./shared";

// The user-guide scenario (tools/Build-UserGuide.ps1), part 1: a fictional PRIMARY school on a DOUBLE shift, set up through the real screens of the
// published program on a temporary database, from the first start to the dashboard. Chapters 6 to 12 are in capture.spec.ts.
const wizard = school.wizard;

test.describe.configure({ mode: "serial" });

const stepTitle = (page: Page, step: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8) => page.getByRole("heading", { level: 2, exact: true, name: wizard.steps[step] });

test("ch2 — first run: create the owner account, the recovery code, the login and lock screens", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await page.goto(baseUrl);
  await expect(page.getByRole("heading", { name: app.setupTitle })).toBeVisible();
  await page.getByLabel(app.username).fill(data.owner.username);
  await page.getByLabel(app.password, { exact: true }).fill(data.owner.password);
  await page.getByLabel(app.confirmPassword).fill(data.owner.password);
  await shot(page, "02-first-run-account");
  await page.getByRole("button", { name: app.createAccount }).click();
  await expect(page.getByRole("heading", { name: app.codeTitle })).toBeVisible();
  await shot(page, "03-recovery-code");
  await page.getByLabel(app.confirmCodeSaved).check();
  await page.getByRole("button", { name: app.continue }).click();
  await expect(stepTitle(page, 1)).toBeVisible();

  // The login screen (after signing out) and the lock screen.
  await page.getByRole("button", { name: school.shell.userMenu }).click();
  await shot(page, "04-user-menu", { window: true });
  await page.getByRole("menuitem", { name: app.logout }).click();
  await expect(page.getByRole("heading", { name: app.loginTitle })).toBeVisible();
  await shot(page, "05-login");
  await page.getByLabel(app.username).fill(data.owner.username);
  await page.getByLabel(app.password, { exact: true }).fill("كلمة-مرور-خاطئة-1");
  await page.getByRole("button", { name: app.loginAction }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await shot(page, "06-login-wrong-password");
  await page.getByRole("button", { name: app.recoveryLink }).click();
  await expect(page.getByRole("heading", { name: app.recoveryTitle })).toBeVisible();
  await shot(page, "07-recovery-form");
  await page.getByRole("button", { name: app.backToLogin }).click();
  await signIn(page);
  await page.getByRole("button", { name: school.shell.userMenu }).click();
  await page.getByRole("menuitem", { name: school.shell.lock }).click();
  await expect(page.getByLabel(app.password, { exact: true })).toBeVisible();
  await shot(page, "08-lock-screen");
});

test("ch5 — the setup wizard of a double-shift primary school", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await page.goto(`${baseUrl}/setup`);
  const next = () => page.getByRole("button", { name: wizard.next }).click();
  const templates = school.templates;
  const system = school.shiftSystem;

  // 1. The school: name, type (primary), principal.
  await expect(stepTitle(page, 1)).toBeVisible();
  await page.getByLabel(wizard.school.name).fill(data.schoolName);
  await page.getByRole("radio", { name: new RegExp(`^${school.profile.schoolTypes.primary}`) }).check();
  await page.getByLabel(wizard.school.principal).fill(data.principal);
  await shot(page, "10-wizard-1-school");
  await next();

  // 2. The year and the semesters: the proposal is accepted; the holidays are added from the Iraqi template.
  await expect(stepTitle(page, 2)).toBeVisible();
  await shot(page, "11-wizard-2-year");
  await next();

  // 3. Working days and the shift system: «مزدوج» with morning and evening timings and the day → session mapping.
  await expect(stepTitle(page, 3)).toBeVisible();
  await shot(page, "12-wizard-3-timing-single", { part: "top" });
  await page.getByRole("radio", { name: new RegExp(`^${system.systems.dual.replace(/[()]/g, "\\$&")}`) }).check();
  await page.locator("#wizard-morning-template").selectOption("single-break-after-3");
  await page.locator("#wizard-evening-template").selectOption("evening-single-break");
  const term1 = page.getByRole("group", { name: `${system.term1}: ${system.chooseMorning}` });
  await term1.getByRole("button", { name: school.scheduleStructure.days.tuesday }).click();
  await page.getByRole("button", { name: system.reverse }).click();
  await shot(page, "13-wizard-3-timing-double", { part: "top" });
  await shot(page, "14-wizard-3-day-mapping", { part: "bottom" });
  await next();

  // 4. Stages and sections: the primary template, two sections per grade.
  await expect(stepTitle(page, 4)).toBeVisible();
  while ((await page.locator(".stage-card").count()) === 0) {
    await page.getByRole("button", { name: templates.preview }).click();
    break;
  }
  await shot(page, "15-wizard-4-stages-template", { part: "top" });
  await page.getByRole("button", { name: templates.preview }).click();
  await page.getByRole("button", { name: templates.apply }).click();
  await expect(page.locator(".stage-card").first()).toBeVisible();
  await shot(page, "16-wizard-4-stages", { part: "top" });
  await next();

  // 5. Subjects and the curriculum: the official suggestion, then one number typed.
  await expect(stepTitle(page, 5)).toBeVisible();
  await page.getByRole("button", { name: templates.preview }).click();
  await shot(page, "17-wizard-5-subjects-preview", { part: "top" });
  await page.getByRole("button", { name: templates.apply }).click();
  await expect(page.locator(".subject-chips .subject-chip").first()).toBeVisible();
  await shot(page, "18-wizard-5-curriculum", { part: "top" });
  // The official study plan of the Ministry fills the weekly lessons of every subject.
  const plan = page.locator(".suggested-panel");
  await plan.getByText(school.suggested.open, { exact: true }).first().click();
  await expect(plan.getByText(school.suggested.provenance)).toBeVisible();
  await shot(page, "18b-wizard-5-official-plan", { target: ".suggested-panel" });
  await plan.getByRole("button", { name: school.suggested.apply }).click();
  await expect(page.getByRole("status").filter({ hasText: /تمت تعبئة المنهج من الخطة الرسمية/ })).toBeVisible();
  await shot(page, "18c-wizard-5-curriculum-filled", { target: ".curriculum-scroll" });
  await next();

  // 6. Teachers: the Excel template, a filled file, the preview and the import.
  await expect(stepTitle(page, 6)).toBeVisible();
  const subjects = (await api<{ items: { name: string }[] }>(page, baseUrl, "GET", "/subjects/?pageSize=100")).items.map((subject) => subject.name);
  const folder = mkdtempSync(join(tmpdir(), "sst-guide-"));
  const header = ["الاسم الكامل (مطلوب)", "الاسم المختصر", "التخصصات (مواد مفصولة بـ «،»)", "الحد الأقصى للحصص يومياً", "الحد الأقصى للحصص أسبوعياً", "ملاحظات"];
  const rows = [
    header,
    ...data.classTeachers.map((name) => [name, "", subjects.join("،"), "", "", "معلمة صف"]),
    ...data.extraTeachers.map((teacher) => [teacher.name, "", subjects.join("،"), "", "", teacher.note]),
    ["", "", "", "", "", "صف ناقص الاسم (للتوضيح)"],
  ];
  const file = writeWorkbook(folder, "teachers-guide", "المعلمون", rows);
  await page.getByRole("button", { name: school.teachers.import.button }).click();
  await shot(page, "19-wizard-6-import-panel", { part: "top" });
  await page.locator("#teachers-import-file").setInputFiles(file);
  await expect(page.getByRole("table", { name: school.teachers.import.preview })).toBeVisible();
  await shot(page, "20-wizard-6-import-preview", { target: ".bulk-panel" });
  await page.getByRole("button", { name: school.teachers.import.apply(`${(data.classTeachers.length + data.extraTeachers.length)}`.replace(/\d/g, (digit) => "٠١٢٣٤٥٦٧٨٩"[Number(digit)])) }).click();
  await expect(page.getByRole("status").filter({ hasText: /اكتمل الاستيراد/ })).toBeVisible();
  await shot(page, "21-wizard-6-teachers-done", { part: "top" });
  await next();

  // 7. Workloads: the table, then «اقتراح تلقائي» fills the empty rows.
  await expect(stepTitle(page, 7)).toBeVisible();
  await shot(page, "22-wizard-7-workload-empty", { part: "top" });
  const suggester = school.workload.suggester;
  await page.getByText(school.workloadTable.suggestTitle).click();
  await page.getByRole("button", { name: suggester.preview }).click();
  await expect(page.locator(".plan-lines").first()).toBeVisible();
  await shot(page, "23-wizard-7-workload-suggest", { part: "top" });
  await page.getByRole("button", { name: suggester.apply }).click();
  await page.getByRole("dialog", { name: suggester.confirmTitle }).getByRole("button", { name: suggester.apply }).click();
  await expect(page.getByRole("status").filter({ hasText: /./ }).first()).toBeVisible();
  await page.waitForLoadState("networkidle");
  await shot(page, "24-wizard-7-workload-filled", { part: "top" });
  await next();

  // 8. Review and finish.
  await expect(stepTitle(page, 8)).toBeVisible();
  await expect(page.locator(".count-grid")).toBeVisible();
  await shot(page, "25-wizard-8-review");
  await page.getByRole("button", { name: wizard.finish }).click();
  await expect(page.getByRole("heading", { name: school.nav.dashboard, level: 1 })).toBeVisible();
});

test("ch3, ch4 — the dashboard and the school profile (logo, principal)", async ({ page }) => {
  await page.clock.setFixedTime(fixedTime);
  await signIn(page);
  await page.goto(`${baseUrl}/`);
  await expect(page.getByRole("heading", { name: school.nav.dashboard, level: 1 })).toBeVisible();
  await page.waitForTimeout(800);
  await shot(page, "30-dashboard", { window: true });
  await page.goto(`${baseUrl}/school/profile`);
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await page.locator("#logo-file").setInputFiles({ name: "logo.png", mimeType: "image/png", buffer: logoPng() });
  await expect(page.getByRole("img", { name: school.profile.imageAlt(school.profile.logo) })).toBeVisible();
  await shot(page, "31-profile");
});
