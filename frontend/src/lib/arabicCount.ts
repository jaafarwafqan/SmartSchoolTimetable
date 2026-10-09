// Arabic counting (spec fix B7). The counted noun agrees with the number:
//   1 -> "مادة واحدة"; 2 -> "مادتان" (nominative) / "مادتين" (oblique);
//   3–10 -> plural ("٣ مواد"); 11–99 -> singular accusative ("١١ مادة"، "١١ صفاً");
//   100 and above follow the last two digits (103 -> plural, 111 -> accusative, 100/101/102 -> singular).
// The number itself is formatted by the caller's formatter so the school's numerals setting is respected.

export type CountForms = {
  /** The noun with "one" after it (counted 1). */
  one: string;
  dualNominative: string;
  dualOblique: string;
  /** Genitive plural used after 3–10. */
  plural: string;
  /** Genitive singular used after 100, 1000 … */
  singular: string;
  /** Accusative singular used after 11–99. */
  accusative: string;
};

export const countNouns = {
  subject: { one: "مادة واحدة", dualNominative: "مادتان", dualOblique: "مادتين", plural: "مواد", singular: "مادة", accusative: "مادة" },
  teacher: { one: "معلم واحد", dualNominative: "معلمان", dualOblique: "معلمين", plural: "معلمين", singular: "معلم", accusative: "معلماً" },
  femaleTeacher: { one: "معلمة واحدة", dualNominative: "معلمتان", dualOblique: "معلمتين", plural: "معلمات", singular: "معلمة", accusative: "معلمة" },
  section: { one: "شعبة واحدة", dualNominative: "شعبتان", dualOblique: "شعبتين", plural: "شعب", singular: "شعبة", accusative: "شعبة" },
  grade: { one: "صف واحد", dualNominative: "صفان", dualOblique: "صفين", plural: "صفوف", singular: "صف", accusative: "صفاً" },
  stage: { one: "مرحلة واحدة", dualNominative: "مرحلتان", dualOblique: "مرحلتين", plural: "مراحل", singular: "مرحلة", accusative: "مرحلة" },
  lesson: { one: "حصة واحدة", dualNominative: "حصتان", dualOblique: "حصتين", plural: "حصص", singular: "حصة", accusative: "حصة" },
  day: { one: "يوم واحد", dualNominative: "يومان", dualOblique: "يومين", plural: "أيام", singular: "يوم", accusative: "يوماً" },
  term: { one: "فصل واحد", dualNominative: "فصلان", dualOblique: "فصلين", plural: "فصول", singular: "فصل", accusative: "فصلاً" },
  minute: { one: "دقيقة واحدة", dualNominative: "دقيقتان", dualOblique: "دقيقتين", plural: "دقائق", singular: "دقيقة", accusative: "دقيقة" },
  change: { one: "تغيير واحد", dualNominative: "تغييران", dualOblique: "تغييرين", plural: "تغييرات", singular: "تغيير", accusative: "تغييراً" },
  assignment: { one: "نصاب واحد", dualNominative: "نصابان", dualOblique: "نصابين", plural: "أنصبة", singular: "نصاب", accusative: "نصاباً" },
  line: { one: "بند واحد", dualNominative: "بندان", dualOblique: "بندين", plural: "بنود", singular: "بند", accusative: "بنداً" },
  error: { one: "خطأ واحد", dualNominative: "خطآن", dualOblique: "خطأين", plural: "أخطاء", singular: "خطأ", accusative: "خطأً" },
  warning: { one: "ملاحظة واحدة", dualNominative: "ملاحظتان", dualOblique: "ملاحظتين", plural: "ملاحظات", singular: "ملاحظة", accusative: "ملاحظة" },
  second: { one: "ثانية واحدة", dualNominative: "ثانيتان", dualOblique: "ثانيتين", plural: "ثوانٍ", singular: "ثانية", accusative: "ثانية" },
  improvement: { one: "تحسين واحد", dualNominative: "تحسينان", dualOblique: "تحسينين", plural: "تحسينات", singular: "تحسين", accusative: "تحسيناً" },
  version: { one: "إصدار واحد", dualNominative: "إصداران", dualOblique: "إصدارين", plural: "إصدارات", singular: "إصدار", accusative: "إصداراً" },
  conflict: { one: "تعارض واحد", dualNominative: "تعارضان", dualOblique: "تعارضين", plural: "تعارضات", singular: "تعارض", accusative: "تعارضاً" },
  pair: { one: "زوج واحد", dualNominative: "زوجان", dualOblique: "زوجين", plural: "أزواج", singular: "زوج", accusative: "زوجاً" },
} as const satisfies Record<string, CountForms>;

export type CountNoun = keyof typeof countNouns;
export type GrammaticalCase = "nominative" | "oblique";

/** "٩ مواد", "مادتان", "١١ مادة" … with the number written by `formatNumber`. */
export function arabicCount(value: number, noun: CountNoun, formatNumber: (value: number) => string, grammaticalCase: GrammaticalCase = "nominative"): string {
  const forms: CountForms = countNouns[noun];
  const count = Math.abs(Math.trunc(value));
  if (count === 1) return forms.one;
  if (count === 2) return grammaticalCase === "oblique" ? forms.dualOblique : forms.dualNominative;
  const lastTwo = count % 100;
  const form = lastTwo >= 3 && lastTwo <= 10 ? forms.plural
    : lastTwo >= 11 && lastTwo <= 99 ? forms.accusative
    : forms.singular;
  return `${formatNumber(value)} ${form}`;
}
