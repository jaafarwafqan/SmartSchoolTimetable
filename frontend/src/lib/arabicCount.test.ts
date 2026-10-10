import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { arabicCount, countNouns, type CountNoun } from "./arabicCount";
import { createFormatter, formatNumber, iraqiMonthNames } from "./format";

const arab = (value: number) => formatNumber(value, "arab");
const latn = (value: number) => formatNumber(value, "latn");

describe("arabicCount", () => {
  it("follows the Arabic rules at every boundary", () => {
    expect(arabicCount(1, "subject", arab)).toBe("مادة واحدة");
    expect(arabicCount(2, "subject", arab)).toBe("مادتان");
    expect(arabicCount(2, "subject", arab, "oblique")).toBe("مادتين");
    expect(arabicCount(3, "subject", arab)).toBe("٣ مواد");
    expect(arabicCount(9, "subject", arab)).toBe("٩ مواد");
    expect(arabicCount(10, "subject", arab)).toBe("١٠ مواد");
    expect(arabicCount(11, "subject", arab)).toBe("١١ مادة");
    expect(arabicCount(99, "grade", arab)).toBe("٩٩ صفاً");
    expect(arabicCount(100, "subject", arab)).toBe("١٠٠ مادة");
    expect(arabicCount(103, "lesson", arab)).toBe("١٠٣ حصص");
    expect(arabicCount(111, "day", arab)).toBe("١١١ يوماً");
    expect(arabicCount(0, "lesson", latn)).toBe("0 حصة");
  });

  it("knows every noun the app counts, in both numeral systems", () => {
    const expected: Record<CountNoun, [string, string, string, string, string]> = {
      subject: ["مادة واحدة", "مادتان", "3 مواد", "11 مادة", "100 مادة"],
      teacher: ["معلم واحد", "معلمان", "3 معلمين", "11 معلماً", "100 معلم"],
      femaleTeacher: ["معلمة واحدة", "معلمتان", "3 معلمات", "11 معلمة", "100 معلمة"],
      section: ["شعبة واحدة", "شعبتان", "3 شعب", "11 شعبة", "100 شعبة"],
      grade: ["صف واحد", "صفان", "3 صفوف", "11 صفاً", "100 صف"],
      stage: ["مرحلة واحدة", "مرحلتان", "3 مراحل", "11 مرحلة", "100 مرحلة"],
      lesson: ["حصة واحدة", "حصتان", "3 حصص", "11 حصة", "100 حصة"],
      day: ["يوم واحد", "يومان", "3 أيام", "11 يوماً", "100 يوم"],
      term: ["فصل واحد", "فصلان", "3 فصول", "11 فصلاً", "100 فصل"],
      minute: ["دقيقة واحدة", "دقيقتان", "3 دقائق", "11 دقيقة", "100 دقيقة"],
      change: ["تغيير واحد", "تغييران", "3 تغييرات", "11 تغييراً", "100 تغيير"],
      assignment: ["نصاب واحد", "نصابان", "3 أنصبة", "11 نصاباً", "100 نصاب"],
      line: ["بند واحد", "بندان", "3 بنود", "11 بنداً", "100 بند"],
      error: ["خطأ واحد", "خطآن", "3 أخطاء", "11 خطأً", "100 خطأ"],
      warning: ["ملاحظة واحدة", "ملاحظتان", "3 ملاحظات", "11 ملاحظة", "100 ملاحظة"],
      pair: ["زوج واحد", "زوجان", "3 أزواج", "11 زوجاً", "100 زوج"],
      second: ["ثانية واحدة", "ثانيتان", "3 ثوانٍ", "11 ثانية", "100 ثانية"],
      improvement: ["تحسين واحد", "تحسينان", "3 تحسينات", "11 تحسيناً", "100 تحسين"],
      version: ["إصدار واحد", "إصداران", "3 إصدارات", "11 إصداراً", "100 إصدار"],
      conflict: ["تعارض واحد", "تعارضان", "3 تعارضات", "11 تعارضاً", "100 تعارض"],
      gap: ["فراغ واحد", "فراغان", "3 فراغات", "11 فراغاً", "100 فراغ"],
      holiday: ["عطلة واحدة", "عطلتان", "3 عطل", "11 عطلة", "100 عطلة"],
      time: ["مرة واحدة", "مرتان", "3 مرات", "11 مرة", "100 مرة"],
    };
    for (const noun of Object.keys(countNouns) as CountNoun[])
      expect([1, 2, 3, 11, 100].map((value) => arabicCount(value, noun, latn)), noun).toEqual(expected[noun]);
    expect(createFormatter({ numeralSystem: "arabicIndic", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" }).count(10, "teacher")).toBe("١٠ معلمين");
  });
});

describe("Iraqi month names", () => {
  it("shows Gregorian dates with the months used in Iraq, numerals unchanged", () => {
    const latin = createFormatter({ numeralSystem: "western", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });
    const arabic = createFormatter({ numeralSystem: "arabicIndic", calendarDisplay: "gregorian", timeZone: "Asia/Baghdad" });
    iraqiMonthNames.forEach((name, index) => {
      const iso = `2026-${String(index + 1).padStart(2, "0")}-15`;
      expect(latin.date(iso)).toContain(name);
      expect(latin.date(iso)).toContain("15");
    });
    expect(latin.date("2026-09-01")).toBe("1 أيلول 2026");
    expect(arabic.date("2027-06-30")).toBe("٣٠ حزيران ٢٠٢٧");
    expect(latin.month("2027-02-01")).toContain("شباط");
    for (const standard of ["سبتمبر", "يونيو", "فبراير", "يناير"]) expect(latin.dateRange("2026-01-01", "2026-12-31")).not.toContain(standard);
  });
});

// Guard: counted phrases must come from arabicCount, never from "${number} noun" in a template string.
const root = resolve(process.cwd(), "src");
const nounForms = [...new Set(Object.values(countNouns).flatMap((forms) => [forms.plural, forms.singular, forms.accusative]))];
const concatenated = new RegExp(`\\$\\{[^}]+\\}\\s*(?:${nounForms.join("|")})(?![\\p{L}])`, "u");

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    return statSync(path).isDirectory() ? sourceFiles(path) : /\.(ts|tsx)$/.test(name) && !/\.test\./.test(name) ? [path] : [];
  });
}

describe("count agreement guard", () => {
  it("finds no number + noun built by concatenation", () => {
    const offenders = sourceFiles(root)
      .filter((path) => !path.endsWith("arabicCount.ts"))
      .flatMap((path) => readFileSync(path, "utf8").split("\n").map((line, index) => ({ path, line, index })))
      .filter(({ line }) => concatenated.test(line))
      .map(({ path, index }) => `${relative(root, path)}:${index + 1}`);
    expect(offenders).toEqual([]);
  });
});
