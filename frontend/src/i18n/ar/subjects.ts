// Phase 2 strings for subjects (2D) and the shared blocked-periods grid. Numbers arrive formatted.
import { isolate } from "../isolate";

export const blockedGrid = {
  title: "الحصص المحجوبة",
  hint: "اختر الخلايا التي لا تُوضع فيها الحصة. استخدم الأسهم للتنقل والمسافة أو Enter للتبديل.",
  noGrid: "عرّف أيام الدوام وحصص الوردية في السنة الحالية أولاً لتحديد الحصص المحجوبة.",
  outsideGrid: "بعض الحصص المحجوبة المحفوظة خارج أيام الدوام أو الحصص الحالية، وستُحذف عند الحفظ.",
  lesson: (number: string) => `ح${number}`,
  cell: (day: string, lesson: string, blocked: boolean) => `${day}، الحصة ${lesson}: ${blocked ? "محجوبة" : "متاحة"}`,
} as const;

export const subjects = {
  title: "المواد",
  description: "المواد الدراسية بألوانها وأولويتها وقيود توزيعها.",
  add: "إضافة مادة",
  edit: "تعديل المادة",
  save: "حفظ المادة",
  saved: "تم حفظ المادة.",
  archived: "تمت أرشفة المادة.",
  restored: "تمت استعادة المادة.",
  deleted: "تم حذف المادة.",
  deleteTitle: "حذف المادة",
  deleteConsequence: "ستُحذف المادة نهائياً. للاحتفاظ بسجلها استخدم الأرشفة.",
  search: "بحث في المواد",
  empty: "لا توجد مواد بعد.",
  name: "اسم المادة",
  color: "لون المادة",
  colorSwatch: (number: string) => `اللون ${number}`,
  priority: "الأولوية",
  priorityHint: "من ١ (الأدنى) إلى ٥ (الأعلى).",
  priorityValue: (number: string) => `الأولوية ${number}`,
  flags: "خصائص التوزيع",
  distributionEnabled: "التوزيع مفعّل",
  spreadAcrossDays: "توزيع الحصص على أيام مختلفة",
  heavy: "مادة ثقيلة",
  requiresDoublePeriod: "تتطلب حصة مزدوجة",
  notes: "ملاحظات (اختياري)",
  blockedCount: "الحصص المحجوبة",
  rowName: (name: string) => isolate(name),
} as const;
