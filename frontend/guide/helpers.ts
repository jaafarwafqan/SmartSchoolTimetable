import type { Page } from "@playwright/test";
import { mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { deflateSync } from "node:zlib";

const here = dirname(fileURLToPath(import.meta.url));
export const repositoryRoot = resolve(here, "../..");
export const imageFolder = join(repositoryRoot, "docs", "guide", "img");
mkdirSync(imageFolder, { recursive: true });
/** Scratch files of a capture run (print PDFs); never committed (.gitignore). */
export const scratchFolder = join(repositoryRoot, "docs", "guide", "tmp");
mkdirSync(scratchFolder, { recursive: true });

/**
 * Replaces this computer's user name wherever it is shown (data-folder and backup-folder paths) by a neutral word, in text and in fields, so no
 * screenshot of the guide carries the name of the person who built it.
 */
export async function hideMachine(page: Page): Promise<void> {
  await page.evaluate(() => {
    const pattern = /([A-Za-z]:\\Users\\)[^\\\s"]+/gi;
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (node.nodeValue && pattern.test(node.nodeValue)) node.nodeValue = node.nodeValue.replace(pattern, "$1USER");
    }
    for (const field of document.querySelectorAll<HTMLInputElement>("input")) {
      if (pattern.test(field.value)) field.value = field.value.replace(pattern, "$1USER");
    }
    for (const element of document.querySelectorAll<HTMLElement>("[title]")) {
      const title = element.getAttribute("title") ?? "";
      if (pattern.test(title)) element.setAttribute("title", title.replace(pattern, "$1USER"));
    }
  });
}

/**
 * One figure of the guide, saved as docs/guide/img/<name>.png (fonts ready, the user name hidden, animations off).
 * By default the main content area of the program is captured whole (so the text stays large when the figure is scaled into the page);
 * `window: true` takes the whole window (the menu and the top bar: for orientation, and for dialogs); `part` keeps the top or the bottom
 * of a very tall area.
 */
export async function shot(page: Page, name: string, options: { window?: boolean; part?: "top" | "bottom"; target?: string; width?: number } = {}): Promise<void> {
  const viewport = page.viewportSize();
  if (options.width && viewport) {
    await page.setViewportSize({ width: options.width, height: viewport.height });
    await page.waitForTimeout(400);
  }
  try {
    await capture(page, name, options);
  } finally {
    if (options.width && viewport) await page.setViewportSize(viewport);
  }
}

async function capture(page: Page, name: string, options: { window?: boolean; part?: "top" | "bottom"; target?: string }): Promise<void> {
  await page.evaluate(async () => { await document.fonts.ready; });
  await hideMachine(page);
  const path = join(imageFolder, `${name}.png`);
  const main = page.locator(".app-main");
  if (options.window || (await main.count()) === 0) {
    await page.screenshot({ path, animations: "disabled", caret: "hide" });
    return;
  }
  // The sticky top bar and menu would be drawn into the middle of a tall capture: hide them (they keep their space).
  await page.evaluate(() => {
    // A constructed stylesheet: the program's Content-Security-Policy does not allow injected <style> elements.
    const sheet = new CSSStyleSheet();
    sheet.replaceSync(".app-topbar, .app-sidebar { visibility: hidden !important; }");
    document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
    (window as unknown as { __guideSheet: CSSStyleSheet }).__guideSheet = sheet;
  });
  try {
    if (options.target) {
      await page.locator(options.target).first().screenshot({ path, animations: "disabled", caret: "hide" });
    } else if (options.part) {
      await main.scrollIntoViewIfNeeded();
      const box = await main.evaluate((element) => { const r = element.getBoundingClientRect(); return { x: r.left + window.scrollX, y: r.top + window.scrollY, width: r.width, height: r.height }; });
      const height = Math.min(box.height, 760);
      const top = options.part === "top" ? 0 : Math.max(0, box.height - height);
      await page.screenshot({ path, fullPage: true, animations: "disabled", caret: "hide", clip: { x: box.x, y: box.y + top, width: box.width, height } });
    } else {
      await main.screenshot({ path, animations: "disabled", caret: "hide" });
    }
  } finally {
    await page.evaluate(() => {
      const sheet = (window as unknown as { __guideSheet?: CSSStyleSheet }).__guideSheet;
      if (sheet) document.adoptedStyleSheets = document.adoptedStyleSheets.filter((item) => item !== sheet);
    });
  }
}

/** A 96×96 PNG logo (a dark blue ring with a star-like dot) made here, so the guide needs no binary file. */
export function logoPng(): Buffer {
  const size = 96;
  const rows: number[] = [];
  for (let y = 0; y < size; y++) {
    rows.push(0);
    for (let x = 0; x < size; x++) {
      const distance = Math.hypot(x - 47.5, y - 47.5);
      const ring = distance > 30 && distance < 44;
      const dot = distance < 9;
      rows.push(ring || dot ? 23 : 255, ring || dot ? 88 : 255, ring || dot ? 166 : 255);
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
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk("IHDR", header), chunk("IDAT", deflateSync(Buffer.from(rows))), chunk("IEND", Buffer.alloc(0))]);
}
