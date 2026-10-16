// Builds the user guide: docs/guide/USER_GUIDE_AR.html (open it in a browser) and docs/USER_GUIDE_AR.pdf (A4, RTL, with a table of contents).
// Run through tools/Build-UserGuide.ps1 (or `node guide/build.mjs` from frontend/ once the figures exist in docs/guide/img).
import { chromium } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { chapters } from "./content.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const guideFolder = join(root, "docs", "guide");
const imageFolder = join(guideFolder, "img");
const htmlPath = join(guideFolder, "USER_GUIDE_AR.html");
const pdfPath = join(root, "docs", "USER_GUIDE_AR.pdf");
const scratch = join(guideFolder, "tmp");
mkdirSync(join(guideFolder, "fonts"), { recursive: true });
mkdirSync(scratch, { recursive: true });
// The program's own font (SIL OFL), copied next to the HTML so the guide needs no network.
const fontSource = join(root, "frontend", "node_modules", "@fontsource-variable", "noto-sans-arabic", "files", "noto-sans-arabic-arabic-wght-normal.woff2");
const fontTarget = join(guideFolder, "fonts", "noto-sans-arabic-arabic-wght-normal.woff2");
if (existsSync(fontSource)) copyFileSync(fontSource, fontTarget);
if (!existsSync(fontTarget)) throw new Error("The Arabic font is missing: run npm ci in frontend/ first.");

const version = /<Version>([^<]+)<\/Version>/.exec(readFileSync(join(root, "Directory.Build.props"), "utf8"))?.[1] ?? "1.0.0";
const dateText = new Date().toLocaleDateString("ar-IQ", { month: "long", year: "numeric" });

const escapeHtml = (text) => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const latin = /[A-Za-z0-9%][A-Za-z0-9.\-_\/\\%:+]*(?: [A-Za-z][A-Za-z0-9.\-_]*)*/g;
const inline = (text) => escapeHtml(text).replace(latin, (run) => `<bdi dir="ltr">${run}</bdi>`).replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
const markers = new Map();
const markerOf = (id) => { if (!markers.has(id)) markers.set(id, markers.size + 1); return `SSTMARK${String(markers.get(id)).padStart(4, "0")}X`; };
const toArabicDigits = (value) => String(value).replace(/\d/g, (digit) => "٠١٢٣٤٥٦٧٨٩"[Number(digit)]);

const icons = {
  tip: '<path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5"/><path d="M9 18h6"/><path d="M10 22h4"/>',
  warn: '<path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  note: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
};
const callout = (kind, text) =>
  `<aside class="callout ${kind}"><svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[kind]}</svg><p>${inline(text)}</p></aside>`;
const calloutTitle = { tip: "نصيحة", warn: "تنبيه", note: "معلومة" };

function renderBlocks(chapter, chapterNumber) {
  let figure = 0;
  const out = [];
  for (const block of chapter.blocks) {
    const [kind, ...rest] = block;
    if (kind === "p") out.push(`<p>${inline(rest[0])}</p>`);
    else if (kind === "h") out.push(`<h3>${inline(rest[0])}</h3>`);
    else if (kind === "steps") out.push(`<ol class="steps">${rest[0].map((item) => `<li>${inline(item)}</li>`).join("")}</ol>`);
    else if (kind === "bullets") out.push(`<ul>${rest[0].map((item) => `<li>${inline(item)}</li>`).join("")}</ul>`);
    else if (kind === "table") {
      const [head, rows] = rest;
      out.push(`<table><thead><tr>${head.map((cell) => `<th>${inline(cell)}</th>`).join("")}</tr></thead><tbody>${rows.map((row) => `<tr>${row.map((cell) => `<td>${inline(cell)}</td>`).join("")}</tr>`).join("")}</tbody></table>`);
    } else if (kind === "fig") {
      const [file, caption, options = {}] = rest;
      if (!existsSync(join(imageFolder, `${file}.png`))) throw new Error(`Missing figure ${file}.png`);
      figure += 1;
      const style = [options.h && options.h <= 130 ? `max-height:${options.h}mm` : "", options.w ? `width:${options.w}%` : ""].filter(Boolean).join(";");
      out.push(`<figure><img src="img/${file}.png" alt="${escapeHtml(caption)}"${style ? ` style="${style}"` : ""}><figcaption><b>الشكل ${toArabicDigits(chapterNumber)}-${toArabicDigits(figure)}:</b> ${inline(caption)}</figcaption></figure>`);
    } else if (calloutTitle[kind]) out.push(callout(kind, rest[0]));
    else throw new Error(`Unknown block ${kind}`);
  }
  return out.join("\n");
}

function sectionsOf(chapter) {
  return chapter.blocks.filter((block) => block[0] === "h").map((block, index) => ({ id: `${chapter.id}-s${index + 1}`, title: block[1] }));
}

function buildHtml(pages) {
  markers.clear();
  const page = (id) => { const found = markers.has(id) ? pages[markers.get(id)] : undefined; return found ? toArabicDigits(found) : ""; };
  const numbered = chapters.filter((chapter) => chapter.id !== "intro");
  const body = chapters.map((chapter) => {
    const number = chapter.id === "intro" ? 0 : numbered.indexOf(chapter) + 1;
    let subIndex = 0;
    const content = renderBlocks(chapter, number).replace(/<h3>/g, () => { subIndex += 1; return `<h3 id="${chapter.id}-s${subIndex}"><span class="mark">${markerOf(`${chapter.id}-s${subIndex}`)}</span>`; });
    return `<section class="chapter" id="${chapter.id}"><header class="chapter-head"><span class="mark">${markerOf(chapter.id)}</span>${number ? `<span class="chapter-number">${toArabicDigits(number)}</span>` : ""}<h2>${inline(chapter.title)}</h2></header>${content}</section>`;
  }).join("\n");
  const toc = chapters.map((chapter) => {
    const number = chapter.id === "intro" ? "" : `${toArabicDigits(numbered.indexOf(chapter) + 1)}. `;
    const subs = sectionsOf(chapter).map((section) => `<li class="toc-sub"><a href="#${section.id}">${inline(section.title)}</a><span class="dots"></span><span class="pg">${page(section.id)}</span></li>`).join("");
    return `<li class="toc-chapter"><a href="#${chapter.id}">${number}${inline(chapter.title)}</a><span class="dots"></span><span class="pg">${page(chapter.id)}</span></li>${subs}`;
  }).join("");
  return `<!doctype html>
<html lang="ar" dir="rtl">
<head>
<meta charset="utf-8">
<title>دليل المستخدم - الجدول الذكي</title>
<style>
@font-face { font-family: "Noto Sans Arabic"; src: url("fonts/noto-sans-arabic-arabic-wght-normal.woff2") format("woff2"); font-weight: 100 900; }
:root { --ink: #12233f; --muted: #50607a; --brand: #1658a6; --line: #cfd8e6; --tip: #e8f1fd; --warn: #fff4dc; --note: #eef1f6; }
* { box-sizing: border-box; }
@page { size: A4; margin: 20mm 17mm 22mm; @bottom-center { content: "صفحة " counter(page); font: 9pt "Noto Sans Arabic"; color: #50607a; } @top-center { content: "الجدول الذكي - دليل المستخدم"; font: 9pt "Noto Sans Arabic"; color: #50607a; } }
@page :first { margin: 0; @bottom-center { content: none; } @top-center { content: none; } }
html { font-family: "Noto Sans Arabic", "Segoe UI", sans-serif; color: var(--ink); font-size: 11pt; line-height: 1.85; }
body { margin: 0; }
.mark { display: block; height: 0; overflow: visible; font-size: 3px; line-height: 0; color: #fff; direction: ltr; text-align: left; white-space: nowrap; user-select: none; }
.cover { height: 297mm; display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; background: linear-gradient(180deg, #f2f6fc 0%, #ffffff 60%); break-after: page; padding: 0 25mm; }
.cover .logo { width: 46mm; height: 46mm; border-radius: 12mm; background: var(--brand); color: #fff; display: grid; place-items: center; margin-bottom: 14mm; }
.cover h1 { font-size: 38pt; margin: 0 0 4mm; line-height: 1.3; }
.cover .subtitle { font-size: 17pt; color: var(--muted); margin: 0 0 22mm; }
.cover .meta { font-size: 12pt; color: var(--muted); line-height: 2; }
.cover .meta b { color: var(--ink); }
.toc { break-after: page; }
.toc h2 { font-size: 22pt; margin: 0 0 8mm; }
.toc ul { list-style: none; margin: 0; padding: 0; }
.toc li { display: flex; align-items: baseline; gap: 6px; }
.toc a { color: inherit; text-decoration: none; }
.toc .dots { flex: 1; border-bottom: 1.5px dotted #9aa8c0; transform: translateY(-4px); }
.toc-chapter { font-weight: 700; margin-top: 3mm; font-size: 12pt; }
.toc-sub { padding-inline-start: 9mm; font-size: 10pt; color: var(--muted); line-height: 1.7; }
.chapter { break-before: page; }
.chapter-head { display: flex; align-items: center; gap: 6mm; border-bottom: 3px solid var(--brand); padding-bottom: 5mm; margin-bottom: 7mm; position: relative; }
.chapter-number { min-width: 18mm; height: 18mm; border-radius: 5mm; background: var(--brand); color: #fff; font-size: 24pt; font-weight: 700; display: grid; place-items: center; }
h2 { font-size: 24pt; margin: 0; line-height: 1.3; }
h3 { font-size: 14pt; color: var(--brand); margin: 7mm 0 2mm; break-after: avoid; position: relative; }
p { margin: 0 0 3mm; }
ul, ol { margin: 0 0 4mm; padding-inline-start: 9mm; }
li { margin-bottom: 1.5mm; }
.steps { list-style: none; counter-reset: step; padding-inline-start: 0; }
.steps li { counter-increment: step; position: relative; padding-inline-start: 11mm; break-inside: avoid; }
.steps li::before { content: counter(step); position: absolute; inset-inline-start: 0; top: 2px; width: 7.5mm; height: 7.5mm; border-radius: 50%; background: var(--brand); color: #fff; font-weight: 700; font-size: 10pt; display: grid; place-items: center; line-height: 1; }
figure { margin: 4mm 0 6mm; text-align: center; break-inside: avoid; }
figure img { max-width: 100%; max-height: 232mm; border: 1px solid var(--line); border-radius: 3mm; object-fit: contain; }
figcaption { font-size: 9.5pt; color: var(--muted); margin-top: 2mm; line-height: 1.6; }
.callout { display: flex; gap: 4mm; align-items: flex-start; padding: 3.5mm 5mm; border-radius: 3mm; margin: 4mm 0; break-inside: avoid; border-inline-start: 1.5mm solid; }
.callout p { margin: 0; flex: 1; }
.callout svg { flex: none; margin-top: 3px; }
.callout.tip { background: var(--tip); border-color: var(--brand); color: #0f3d78; }
.callout.warn { background: var(--warn); border-color: #c77d00; color: #6a4300; }
.callout.note { background: var(--note); border-color: #7b8aa3; color: #2d3b55; }
table { width: 100%; border-collapse: collapse; margin: 3mm 0 5mm; font-size: 10pt; line-height: 1.7; }
th, td { border: 1px solid var(--line); padding: 2mm 3mm; vertical-align: top; text-align: start; }
th { background: #eaf0f9; }
tr { break-inside: avoid; }
strong { font-weight: 700; }
</style>
</head>
<body>
<section class="cover">
  <div class="logo"><svg viewBox="0 0 24 24" width="30mm" height="30mm" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21.42 10.922a1 1 0 0 0-.019-1.838L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.832l8.57 3.908a2 2 0 0 0 1.66 0z"/><path d="M22 10v6"/><path d="M6 12.5V16a6 3 0 0 0 12 0v-3.5"/></svg></div>
  <h1>الجدول الذكي</h1>
  <p class="subtitle">دليل المستخدم</p>
  <div class="meta"><div><b>الإصدار:</b> ${toArabicDigits(version)}</div><div><b>التاريخ:</b> ${dateText}</div><div><b>المطوّر:</b> جعفر وفقان</div><div>ويندوز 10 (64-بت) أو أحدث · يعمل دون إنترنت</div></div>
</section>
<section class="toc"><h2>المحتويات</h2><ul>${toc}</ul></section>
${body}
</body>
</html>`;
}

async function renderPdf(html, pdf) {
  writeFileSync(htmlPath, html, "utf8");
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.goto(pathToFileURL(htmlPath).href);
    await page.evaluate(async () => { await document.fonts.ready; });
    await page.pdf({ path: pdf, format: "A4", printBackground: true, preferCSSPageSize: true });
  } finally {
    await browser.close();
  }
}

// Two passes: the first finds the page of every heading, the second prints them in the table of contents.
const firstPass = join(scratch, "guide-pass1.pdf");
await renderPdf(buildHtml({}), firstPass);
const pages = JSON.parse(execFileSync("python", [join(root, "tools", "guide", "find_pages.py"), firstPass], { encoding: "utf8" }));
await renderPdf(buildHtml(pages), pdfPath);
const check = JSON.parse(execFileSync("python", [join(root, "tools", "guide", "find_pages.py"), pdfPath], { encoding: "utf8" }));
for (const key of Object.keys(pages)) if (pages[key] !== check[key]) throw new Error(`The table of contents moved a heading (${key}: ${pages[key]} -> ${check[key]}).`);
console.log(`USER_GUIDE_AR.pdf: ${check.__count} pages`);
