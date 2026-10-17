import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import type { KioskData } from "../src/features/kiosk/kiosk";
import { ApiServer } from "./support/apiServer";
import { api } from "./support/api";
import { seedReadySchool } from "./support/readySchool";
import { expectNoLatinText, expectNoPageScrollX, expectNoSeriousA11yViolations, setupOwner } from "./support/flows";

const server = new ApiServer();
const text = messages.school.sharing;
const kiosk = messages.school.kioskScreen;
const keyPassword = "كلمة-مرور-المفتاح-2026";

type Run = { id: number; status: string; timetableVersionId: number | null };
type Summary = { id: number; version: number };

/** A ready school with one generated and approved version (through the real endpoints). */
async function approvedSchool(page: Page): Promise<number> {
  const yearId = await seedReadySchool(page, server.baseUrl);
  const started = await api<Run>(page, server.baseUrl, "POST", `/academic-years/${yearId}/generation/runs`, { timeLimitSeconds: 10, deterministic: true, seed: 42 });
  let run = started;
  for (let attempt = 0; attempt < 120 && !["completed", "infeasible", "failed", "cancelled", "timedOut"].includes(run.status); attempt++) {
    await page.waitForTimeout(1_000);
    run = await api<Run>(page, server.baseUrl, "GET", `/generation/runs/${started.id}`);
  }
  expect(run.status).toBe("completed");
  const summary = (await api<{ summary: Summary }>(page, server.baseUrl, "GET", `/timetables/${run.timetableVersionId}`)).summary;
  await api(page, server.baseUrl, "POST", `/timetables/${summary.id}/approve`, { version: summary.version });
  return yearId;
}

test("(9a-2) «مشاركة الجدول مع الهاتف»: codes, the key copy right after the key is made, files, cards and the app link", async ({ browser, page }) => {
  test.setTimeout(300_000);
  await server.start(browser, "phase9-sharing");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Sharing-Owner-1");
    await approvedSchool(page);
    await page.goto(`${server.baseUrl}/timetable/share`);
    await expect(page.getByRole("heading", { name: text.title, level: 1 })).toBeVisible();
    await expect(page.getByRole("link", { name: messages.school.nav.share })).toBeVisible();
    await expect(page.getByText(text.key.none)).toBeVisible();
    await expect(page.getByText(text.principal.privacy)).toBeVisible();

    // The first code makes the school key, and the page then asks for its password-protected copy.
    await page.getByRole("button", { name: text.pairing.show }).click();
    await expect(page.getByRole("img", { name: text.pairing.alt })).toBeVisible();
    const backup = page.getByRole("dialog", { name: text.backupDialog.title });
    await expect(backup).toBeVisible();
    await expect(backup.getByText(text.backupDialog.firstTime)).toBeVisible();
    await expect(backup.getByText(text.backupDialog.warning)).toBeVisible();
    await backup.getByLabel(text.backupDialog.password, { exact: true }).fill(keyPassword);
    await backup.getByLabel(text.backupDialog.confirm).fill(`${keyPassword}x`);
    await backup.getByRole("button", { name: text.backupDialog.save }).click();
    await expect(backup.getByText(messages.app.validationConfirm)).toBeVisible();
    await backup.getByLabel(text.backupDialog.confirm).fill(keyPassword);
    const keyDownload = page.waitForEvent("download");
    await backup.getByRole("button", { name: text.backupDialog.save }).click();
    expect((await keyDownload).suggestedFilename()).toMatch(/^school-key-[0-9A-F]{4}(-[0-9A-F]{4}){3}\.sstkey$/);
    await expect(backup.getByText(text.backupDialog.saved)).toBeVisible();
    await backup.getByRole("button", { name: text.backupDialog.done }).click();
    await expect(page.getByText(text.key.notBackedUp)).toHaveCount(0);

    // One teacher: the code on the screen and the file named after the teacher.
    const choice = page.getByLabel(text.teacher.choose, { exact: true });
    const options = await choice.locator("option").allTextContents();
    await choice.selectOption({ index: 1 });
    const teacherName = options[1].split(" — ")[0];
    await page.getByRole("button", { name: text.teacher.show }).click();
    await expect(page.getByRole("img", { name: new RegExp(teacherName) })).toBeVisible();
    const teacherFile = page.waitForEvent("download");
    await page.getByRole("button", { name: text.teacher.download }).click();
    expect((await teacherFile).suggestedFilename()).toBe(`${teacherName}-v1.sst`);
    const zip = page.waitForEvent("download");
    await page.getByRole("button", { name: text.teacher.all }).click();
    expect((await zip).suggestedFilename()).toBe("teachers-v1.zip");
    const principal = page.waitForEvent("download");
    await page.getByRole("button", { name: text.principal.download }).click();
    expect((await principal).suggestedFilename()).toBe("principal-v1.sst");

    // The «حمّل التطبيق» link: a plain http link is refused with the Arabic reason, a full https link is saved.
    const link = page.getByLabel(text.cards.appLink);
    await link.fill("http://t.me/example");
    await page.getByRole("button", { name: text.cards.saveLink }).click();
    await expect(page.locator("#share-app-link-error")).toBeVisible();
    await link.fill("https://t.me/example_school_app");
    await page.getByRole("button", { name: text.cards.saveLink }).click();
    await expect(page.getByText(text.cards.linkSaved)).toBeVisible();

    await expectNoSeriousA11yViolations(page, "sharing page");
    // Data and codes only: the username, the key fingerprint (hexadecimal) and the file/code names allowed in the dictionary.
    const { key } = await api<{ key: { fingerprint: string } }>(page, server.baseUrl, "GET", "/sharing/");
    const fingerprint = key.fingerprint;
    await expectNoLatinText(page, "sharing page", ["https://t.me/example_school_app", "owner", fingerprint, "sstkey", "sst", "zip", "QR", "USB", "A4"]);
    await page.setViewportSize({ width: 375, height: 900 });
    await expectNoPageScrollX(page, "sharing page 375");
    await page.setViewportSize({ width: 1440, height: 900 });

    // Cards: printing is intercepted; in print media every teacher has a card and the teacher code is at least 6 cm wide.
    await page.evaluate(() => { (window as unknown as { printed: number }).printed = 0; window.print = () => { (window as unknown as { printed: number }).printed++; }; });
    await page.getByRole("button", { name: text.cards.print }).click();
    await expect.poll(() => page.evaluate(() => (window as unknown as { printed: number }).printed)).toBe(1);
    await page.emulateMedia({ media: "print" });
    const cards = page.locator(".cards-document .teacher-card");
    await expect(cards).toHaveCount(options.length - 1);
    const width = await cards.first().locator(".teacher-card-main svg.qr-code").evaluate((svg) => svg.getBoundingClientRect().width);
    expect(width).toBeGreaterThanOrEqual(6 * 96 / 2.54 - 1);
    // Two numbered codes (١ تحميل التطبيق، ٢ جدولي) and the optional, smaller pairing code.
    await expect(cards.first().getByText(text.cards.step("١", text.cards.step1))).toBeVisible();
    await expect(cards.first().getByText(text.cards.step("٢", text.cards.step2))).toBeVisible();
    await expect(cards.first().getByRole("img", { name: text.cards.step1 })).toBeVisible();
    const optional = await cards.first().getByRole("img", { name: text.cards.optionalPairing }).evaluate((svg) => svg.getBoundingClientRect().width);
    expect(optional).toBeLessThan(width / 2);
    await page.emulateMedia({ media: "screen" });
    await page.evaluate(() => window.dispatchEvent(new Event("afterprint")));
    await expect(page.locator(".cards-document")).toHaveCount(0);

    // Every export, code and print is in the history.
    const history = await api<{ items: Array<{ eventType: string }> }>(page, server.baseUrl, "GET", "/audit/?category=sharing&pageSize=50");
    expect(new Set(history.items.map((item) => item.eventType))).toEqual(new Set([
      "SharingKeyCreated", "SharingPairingShown", "SharingKeyBackedUp", "SharingTeacherQrShown", "SharingTeacherExported", "SharingAllTeachersExported",
      "SharingPrincipalExported", "SharingSettingsUpdated", "SharingCardsPrinted",
    ]));
  } finally {
    await server.stop();
  }
});

const fixedDay: KioskData = {
  schoolName: "مدرسة الاختبار", date: "2026-11-01", timeZone: "Asia/Baghdad", status: "school", dayOffTitle: null, versionNumber: 3, semester: 1, session: 1,
  periods: [
    { number: 1, startMinute: 480, endMinute: 525 }, { number: 2, startMinute: 530, endMinute: 575 }, { number: null, startMinute: 575, endMinute: 590 },
    { number: 3, startMinute: 590, endMinute: 635 },
  ],
  sections: [{ index: 0, stageName: "الأول المتوسط", label: "أ" }, { index: 1, stageName: "الأول المتوسط", label: "ب" }],
  lessons: [
    { section: 0, lesson: 1, subject: "الرياضيات", colorIndex: 1, teacher: "أحمد" }, { section: 1, lesson: 1, subject: "العلوم", colorIndex: 3, teacher: "زينب" },
    { section: 0, lesson: 2, subject: "اللغة العربية", colorIndex: 2, teacher: "سعد" }, { section: 1, lesson: 2, subject: "الرياضيات", colorIndex: 1, teacher: "أحمد" },
  ],
};

test("(9a-2) «شاشة العرض»: opens with its own key, shows now and next per section, light and dark, accessible", async ({ browser, page, context }) => {
  test.setTimeout(300_000);
  await server.start(browser, "phase9-kiosk");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Kiosk-Owner-1");
    await approvedSchool(page);
    await page.goto(`${server.baseUrl}/timetable/share`);
    const popupPromise = context.waitForEvent("page");
    await page.getByRole("button", { name: text.kiosk.open }).click();
    const screen = await popupPromise;
    await screen.waitForLoadState();
    // The key leaves the address bar; today's real status is shown (the date of the run decides which one).
    await expect.poll(() => new URL(screen.url()).hash).toBe("");
    await expect(screen.getByRole("heading", { name: "مدرسة الاختبار", level: 1 })).toBeVisible();
    await expect(screen.locator(".kiosk-banner, .kiosk-table").first()).toBeVisible();
    await expectNoSeriousA11yViolations(screen, "kiosk (live)");
    await screen.close();

    // Fixed data and a fixed clock: 08:10 in Baghdad is lesson 1, lesson 2 next.
    const fixed = await context.newPage();
    await fixed.route("**/api/v1/kiosk/today", (route) => route.fulfill({ json: fixedDay }));
    await fixed.clock.setFixedTime(new Date("2026-11-01T05:10:00Z"));
    await fixed.goto(`${server.baseUrl}/kiosk#k=abcdefghijklmnopqrstuvwxyz012345`);
    const board = fixed.getByRole("table");
    await expect(board.getByRole("columnheader", { name: new RegExp(kiosk.now) })).toBeVisible();
    await expect(board.getByRole("row", { name: /الأول المتوسط أ/ })).toContainText("الرياضيات");
    await expect(board.getByRole("row", { name: /الأول المتوسط أ/ })).toContainText("اللغة العربية");
    // The screen respects reduced motion: with it on, switching the theme has no colour transition to wait for (the axe helper
    // turns the emulation off again when it finishes, so it is set before every switch).
    for (const theme of [kiosk.themeDark, kiosk.themeLight]) {
      await fixed.emulateMedia({ reducedMotion: "reduce" });
      await fixed.getByRole("button", { name: theme }).click();
      await expect(fixed.locator("html")).toHaveAttribute("data-theme", theme === kiosk.themeDark ? "dark" : "light");
      await expectNoSeriousA11yViolations(fixed, `kiosk ${theme}`);
    }
    await fixed.setViewportSize({ width: 1920, height: 1080 });
    await expectNoPageScrollX(fixed, "kiosk 1920");
    await fixed.setViewportSize({ width: 768, height: 1024 });
    await expectNoPageScrollX(fixed, "kiosk 768");

    // A stopped screen says so in Arabic.
    await fixed.unroute("**/api/v1/kiosk/today");
    await api(page, server.baseUrl, "POST", "/sharing/kiosk/stop");
    const stopped = await context.newPage();
    await stopped.goto(`${server.baseUrl}/kiosk#k=abcdefghijklmnopqrstuvwxyz012345`);
    await expect(stopped.getByText(messages.errors.KIOSK_KEY_INVALID)).toBeVisible();
  } finally {
    await server.stop();
  }
});
