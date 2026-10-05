// Phase 3 strings: resources, the subject's required resource, teacher specializations and the scheduling profile.
// Numbers arrive formatted; user-entered names are bidi-isolated.
import { isolate } from "../isolate";

export const resources = {
  title: "الموارد",
  description: "المختبرات والساحات والقاعات المشتركة، وعدد الشعب التي يمكنها استخدام كل مورد في الحصة نفسها.",
  add: "إضافة مورد",
  addButton: "إضافة المورد",
  quickAddHint: "اكتب الاسم واختر النوع والسعة ثم اضغط مفتاح الإدخال.",
  name: "اسم المورد",
  kind: "النوع",
  capacity: "السعة",
  capacityHint: "عدد الشعب التي تستخدم المورد في الحصة نفسها.",
  capacityValue: (count: string) => `السعة: ${count}`,
  decreaseCapacity: "إنقاص السعة",
  increaseCapacity: "زيادة السعة",
  kinds: { lab: "مختبر", field: "ساحة", hall: "قاعة", other: "أخرى" },
  notes: "ملاحظات (اختياري)",
  search: "بحث في الموارد",
  empty: "لا توجد موارد بعد. أضف مختبراً أو ساحة إذا احتاجتها بعض المواد.",
  added: (name: string) => `أُضيف المورد ${isolate(name)}.`,
  saved: "حُفظ المورد.",
  save: "حفظ المورد",
  archived: "أُرشف المورد.",
  restored: "استُعيد المورد.",
  deleted: "حُذف المورد.",
  deleteTitle: "حذف المورد",
  deleteConsequence: "سيُحذف المورد نهائياً ولا يمكن التراجع عن ذلك.",
} as const;

export const requiredResource = {
  label: "المورد المطلوب",
  hint: "تُجدول حصص هذه المادة في هذا المورد فقط، ضمن سعته في كل حصة.",
  none: "لا يتطلب مورداً",
  archivedOption: (name: string) => `${isolate(name)} (مؤرشف)`,
  badge: (name: string) => `يتطلب ${isolate(name)}`,
} as const;

export const specializations = {
  legend: "التخصصات",
  hint: "المواد التي يدرّسها المعلم. يمكن تعيينه لمادة أخرى مع تنبيه.",
  noSubjects: "أضف المواد أولاً لتختار التخصصات.",
  summary: (subjects: string) => `التخصصات: ${subjects}`,
  none: "بلا تخصصات",
} as const;

/** Shown in a teacher or subject editor whose blocked periods include slots outside the current grid. */
export const orphanOnSave = (count: string) => `عدد الحصص المحجوبة الواقعة خارج الجدول الحالي: ${count}. ستُزال عند الحفظ.`;

export const schedulingProfile = {
  title: "ملف الجدولة",
  description: "قواعد مرنة يوازن بينها التوليد بعد احترام القيود الإلزامية دائماً. الوزن من صفر إلى مئة؛ الأعلى أهم.",
  rules: {
    spreadSubjectsAcrossDays: "توزيع حصص المادة على أيام الأسبوع",
    avoidTeacherGaps: "تقليل الفراغات بين حصص المعلم",
    heavySubjectsEarly: "المواد الثقيلة في الحصص الأولى",
    avoidSameSubjectRepeated: "تجنّب تكرار المادة في اليوم نفسه",
    keepDoubleLessonsTogether: "إبقاء الحصة المزدوجة متتالية",
  },
  enabled: "مفعّلة",
  weight: "الوزن",
  weightOf: (rule: string) => `الوزن: ${rule}`,
  defaultWeight: (weight: string) => `الافتراضي: ${weight}`,
  save: "حفظ ملف الجدولة",
  saved: "حُفظ ملف الجدولة.",
  profileVersion: (version: string) => `إصدار الملف: ${version}`,
  isDefault: "الإعدادات الافتراضية مطبّقة.",
  restore: "استعادة الإعدادات الافتراضية",
  restoreTitle: "استعادة الإعدادات الافتراضية",
  restoreConsequence: "ستعود كل القواعد مفعّلة بأوزانها الافتراضية، ويُنشأ إصدار جديد للملف.",
  restored: "استُعيدت الإعدادات الافتراضية.",
} as const;
