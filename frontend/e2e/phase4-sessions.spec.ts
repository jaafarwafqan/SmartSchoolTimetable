import { expect, test } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { expectNo24HourTimes, expectNoPageScrollX, expectNoSeriousA11yViolations, fillTime, setupOwner } from "./support/flows";
import { seedReadySchool } from "./support/readySchool";

const server = new ApiServer();
const sessions = messages.school.sessions;
const system = messages.school.shiftSystem;
const breaksText = messages.school.scheduleStructure.breaks;
const days = messages.school.scheduleStructure.days;
const generation = messages.school.generation;
const timetable = messages.school.timetable;

/**
 * R3 «دوام مزدوج»: the owner turns on the double session, sets the evening timing with a break, maps the days of
 * semester 1 (Sunday and Monday morning) and reverses them for semester 2 in one click; ONE timetable is generated,
 * and the viewer, print header and Excel show the clock of each day's session in the chosen semester (12-hour).
 */
test("(R3, MF7) double shift: one card, map the days, generate once, read semester 1 and 2", async ({ browser, page }, testInfo) => {
  test.setTimeout(240_000);
  await server.start(browser, "phase4-sessions");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Sessions-Owner-1");
    await seedReadySchool(page, server.baseUrl);

    // MF7: one card «نظام الدوام والأوقات»; «مزدوج» shows the morning and evening timings side by side.
    await page.goto(`${server.baseUrl}/school/timing`);
    const card = page.locator(".page-card", { has: page.getByRole("heading", { name: system.title }) });
    await expect(card).toBeVisible();
    await expect(page.getByText(sessions.unavailable)).toHaveCount(0);
    await card.getByRole("radio", { name: system.systems.dual }).check();

    // Evening timing: 1:00 pm, a break after lesson 3; the lesson count is the morning's.
    const evening = card.locator(".shift-system-block").nth(1);
    await fillTime(evening, system.firstStart(system.sessions.evening), "13:00");
    await evening.getByRole("button", { name: breaksText.add }).click();
    await expect(evening.getByRole("list", { name: system.preview(system.sessions.evening) })).toContainText("١:٠٠ م");
    await expectNo24HourTimes(page, "evening timing");

    // Semester 1: Sunday and Monday morning (Tuesday is turned off); semester 2 is the reverse, in one click.
    const term1 = card.getByRole("group", { name: `${system.term1}: ${system.chooseMorning}` });
    const term2 = card.getByRole("group", { name: `${system.term2}: ${system.chooseMorning}` });
    if ((await term1.getByRole("button", { name: days.tuesday }).getAttribute("aria-pressed")) === "true")
      await term1.getByRole("button", { name: days.tuesday }).click();
    await expect(term1.getByRole("button", { name: days.sunday })).toHaveAttribute("aria-pressed", "true");
    await expect(term1.getByRole("button", { name: days.tuesday })).toHaveAttribute("aria-pressed", "false");
    await card.getByRole("button", { name: system.reverse }).click();
    for (const day of [days.tuesday, days.wednesday, days.thursday])
      await expect(term2.getByRole("button", { name: day })).toHaveAttribute("aria-pressed", "true");
    await expect(term2.getByRole("button", { name: days.sunday })).toHaveAttribute("aria-pressed", "false");
    await expect(card).toContainText(system.eveningDays([days.tuesday, days.wednesday, days.thursday].join(system.daysSeparator)));
    await expectNoSeriousA11yViolations(page, "shift system card");
    for (const width of [375, 1024, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoPageScrollX(page, `shift system at ${width}px`);
    }
    await page.setViewportSize({ width: 1280, height: 900 });
    await card.screenshot({ path: testInfo.outputPath("mf7-shift-system-card.png") });
    await card.getByRole("button", { name: system.save }).click();
    await expect(card.getByText(system.saved)).toBeVisible();

    // Generate ONCE.
    await page.goto(`${server.baseUrl}/timetable/generate`);
    await expect(page.getByText(generation.readinessReady)).toBeVisible();
    await page.getByLabel(generation.timeLimit).selectOption("10");
    await page.getByRole("button", { name: generation.start }).click();
    await expect(page.getByText(generation.statuses.completed)).toBeVisible({ timeout: 90_000 });
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.getByText(timetable.verified)).toBeVisible();

    // Semester 1: Sunday is morning (8:00 am), the clock rows name both sessions. The page opens on the semester whose
    // dates contain today (#86), so the test chooses semester 1 itself rather than depend on the real date.
    const grid = page.locator(".ui-tt-grid").first();
    const sunday = grid.locator("tbody tr").first().locator("th");
    const terms = page.getByRole("group", { name: timetable.termLabel });
    await expect(terms.getByRole("button", { pressed: true })).toHaveCount(1);
    await terms.getByRole("button", { name: timetable.terms[1] }).click();
    await expect(terms.getByRole("button", { name: timetable.terms[1] })).toHaveAttribute("aria-pressed", "true");
    await expect(sunday).toContainText(timetable.sessionShort.morning);
    await expect(grid.locator("thead")).toContainText(timetable.sessionNames.morning);
    await expect(grid.locator("thead")).toContainText(timetable.sessionNames.evening);
    await expect(grid.locator("thead")).toContainText("٨:٠٠ ص");
    await expect(grid.locator("thead")).toContainText("١:٠٠ م");
    const firstSemester = await grid.locator("tbody").innerText();
    await expectNo24HourTimes(page, "section timetable, semester 1");
    await page.locator(".timetable-main").screenshot({ path: testInfo.outputPath("r3-viewer-term1.png") });

    // Semester 2: the same lessons; Sunday is now evening.
    await page.getByRole("group", { name: timetable.termLabel }).getByRole("button", { name: timetable.terms[2] }).click();
    await expect(sunday).toContainText(timetable.sessionShort.evening);
    expect((await grid.locator("tbody").innerText()).replace(/صباحي|مسائي/g, "")).toBe(firstSemester.replace(/صباحي|مسائي/g, ""));
    await expectNo24HourTimes(page, "section timetable, semester 2");
    await expectNoSeriousA11yViolations(page, "timetable viewer, semester 2");
    await page.locator(".timetable-main").screenshot({ path: testInfo.outputPath("r3-viewer-term2.png") });

    await page.getByLabel(timetable.viewsLabel).selectOption("master");
    const master = page.getByRole("table", { name: timetable.views.master });
    await expect(master.locator("thead")).toContainText(timetable.dayWithSession(days.sunday, timetable.sessionShort.evening));
    await expect(master.locator("thead")).toContainText("١:٠٠ م");
    await expectNo24HourTimes(page, "master timetable, semester 2");

    // Print names the semester; Excel exports the chosen semester.
    await page.emulateMedia({ media: "print" });
    await expect(page.locator(".timetable-print-header")).toContainText(timetable.printSemester(timetable.terms[2]));
    await page.emulateMedia({ media: "screen" });
    const download = page.waitForEvent("download");
    await page.getByRole("link", { name: timetable.exportExcel }).click();
    expect((await download).suggestedFilename()).toMatch(/^timetable-v\d+-term2\.xlsx$/);
  } finally {
    await server.stop();
  }
});
