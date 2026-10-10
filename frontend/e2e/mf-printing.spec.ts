import { deflateSync } from "node:zlib";
import { expect, test, type Page } from "@playwright/test";
import { messages } from "../src/i18n/messages";
import { ApiServer } from "./support/apiServer";
import { seedReadySchool } from "./support/readySchool";
import { goToSection, setupOwner } from "./support/flows";

const server = new ApiServer();
const generation = messages.school.generation;
const printing = messages.school.printing;
const profileText = messages.school.profile;

/** A 64×64 PNG logo (a dark ring on white), built here so the test needs no binary fixture. */
function logoPng(): Buffer {
  const size = 64;
  const rows: number[] = [];
  for (let y = 0; y < size; y++) {
    rows.push(0);
    for (let x = 0; x < size; x++) {
      const distance = Math.hypot(x - 31.5, y - 31.5);
      const ink = distance > 20 && distance < 29;
      rows.push(ink ? 20 : 255, ink ? 60 : 255, ink ? 110 : 255);
    }
  }
  const crcTable = Array.from({ length: 256 }, (_, n) => {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    return c >>> 0;
  });
  const crc = (bytes: Buffer) => {
    let c = 0xffffffff;
    for (const byte of bytes) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8);
    return (c ^ 0xffffffff) >>> 0;
  };
  const chunk = (type: string, data: Buffer) => {
    const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
    const out = Buffer.alloc(body.length + 8);
    out.writeUInt32BE(data.length, 0);
    body.copy(out, 4);
    out.writeUInt32BE(crc(body), body.length + 4);
    return out;
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header.set([8, 2, 0, 0, 0], 8);
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk("IHDR", header),
    chunk("IDAT", deflateSync(Buffer.from(rows))), chunk("IEND", Buffer.alloc(0))]);
}

/** Pages in a PDF Chromium wrote (each page object is «/Type /Page», the tree root is «/Type /Pages»). */
function pdfPages(pdf: Buffer): number {
  return (pdf.toString("latin1").match(/\/Type\s*\/Page(?!s)/g) ?? []).length;
}

async function choosePrint(page: Page, scope: keyof typeof printing.scopes) {
  await page.locator("#print-scope").selectOption(scope);
}

test("(MF5) official printing: header, signature footer, A4/A3, portrait/landscape, fit to page, batch pages", async ({ browser, page }, testInfo) => {
  test.setTimeout(240_000);
  await server.start(browser, "mf-printing");
  try {
    await setupOwner(page, server.baseUrl, "owner", "Printing-Owner-1");
    await seedReadySchool(page, server.baseUrl);

    // The header and footer come from the school profile: the logo and the principal's name. The seed changed the
    // profile behind the open page, so reload first (otherwise the form opens on the cached version and saving conflicts).
    await page.reload();
    await goToSection(page, messages.school.nav.profile);
    await page.getByLabel(profileText.principalName).fill("أ. زينب كاظم");
    await page.getByRole("button", { name: profileText.save }).click();
    await expect(page.getByRole("status").filter({ hasText: profileText.saved })).toBeVisible();
    await page.locator("#logo-file").setInputFiles({ name: "logo.png", mimeType: "image/png", buffer: logoPng() });
    await expect(page.getByRole("img", { name: profileText.imageAlt(profileText.logo) })).toBeVisible();

    await page.goto(`${server.baseUrl}/timetable/generate`);
    await expect(page.getByText(generation.readinessReady)).toBeVisible();
    await page.getByLabel(generation.timeLimit).selectOption("10");
    await page.getByRole("button", { name: generation.start }).click();
    await expect(page.getByRole("heading", { name: generation.resultTitle })).toBeVisible({ timeout: 90_000 });
    await page.getByRole("link", { name: generation.openTimetable }).click();
    await expect(page.locator(".timetable-main .ui-tt-grid")).toHaveCount(1);

    // The panel: defaults per job (section A4 landscape, teacher A4 portrait, school A3 landscape), fit on.
    // The print document exists only while the panel is open (or while the browser prints).
    await expect(page.locator(".print-document")).toHaveCount(0);
    await page.getByText(printing.title).click();
    await expect(page.locator("#print-paper")).toHaveValue("A4");
    await expect(page.locator("#print-orientation")).toHaveValue("landscape");
    await choosePrint(page, "teachers");
    await expect(page.locator("#print-orientation")).toHaveValue("portrait");
    await choosePrint(page, "school");
    await expect(page.locator("#print-paper")).toHaveValue("A3");
    await expect(page.locator("#print-orientation")).toHaveValue("landscape");
    await expect(page.getByLabel(printing.fit)).toBeChecked();

    // Print media shows only the document: every page has the full header and the signature footer.
    await page.emulateMedia({ media: "print" });
    await expect(page.locator("#root")).toBeHidden();
    const header = page.locator(".print-page-header").first();
    for (const part of [printing.documentTitle, "مدرسة الاختبار", printing.school]) await expect(header).toContainText(part);
    await expect(header.locator(".print-logo")).toBeVisible();
    expect(await header.locator(".print-logo").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBe(64);
    const footer = page.locator(".print-page-footer").first();
    for (const part of [printing.principal("أ. زينب كاظم"), printing.signature, printing.stamp]) await expect(footer).toContainText(part);
    // page.pdf() prints with print CSS only when no screen media is emulated.
    await page.emulateMedia({ media: null });

    const jobs: Array<{ name: string; scope: keyof typeof printing.scopes; view?: string; pages: number }> = [
      { name: "a3-landscape-school", scope: "school", pages: 1 },
      { name: "a4-landscape-sections", scope: "sections", pages: 2 },
      { name: "a4-portrait-teachers", scope: "teachers", pages: 5 },
    ];
    for (const job of jobs) {
      await choosePrint(page, job.scope);
      await expect(page.locator(".print-document")).toHaveAttribute("data-pages", String(job.pages));
      const pdf = await page.pdf({ path: testInfo.outputPath(`mf5-${job.name}.pdf`), preferCSSPageSize: true, printBackground: false });
      expect(pdfPages(pdf), job.name).toBe(job.pages);
    }

    // Never cut: at the A4-portrait size, no printed grid is wider than its page.
    await choosePrint(page, "sections");
    await page.locator("#print-orientation").selectOption("portrait");
    await page.emulateMedia({ media: "print" });
    await page.setViewportSize({ width: 718, height: 1000 }); // 190 mm of printable width at 96 dpi
    for (const grid of await page.locator(".print-grid .ui-tt-grid").all()) {
      expect(await grid.evaluate((table) => table.scrollWidth <= (table.closest(".print-page") as HTMLElement).clientWidth + 1)).toBe(true);
    }
    await page.emulateMedia({ media: null });
    const pdf = await page.pdf({ path: testInfo.outputPath("mf5-a4-portrait-sections.pdf"), preferCSSPageSize: true, printBackground: false });
    expect(pdfPages(pdf)).toBe(2);
  } finally {
    await server.stop();
  }
});
