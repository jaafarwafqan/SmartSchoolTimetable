// MF2 strings: the flat «الأنصبة» table of the setup wizard (section × subject × teacher). Numbers arrive pre-formatted.
import { isolate } from "../isolate";

export const workloadTable = {
  summary: (assigned: string, rows: string, lessons: string, total: string) =>
    `بنود لها معلم: ${assigned} من ${rows}. الحصص الأسبوعية المسندة: ${lessons} من ${total}.`,
  noRows: "لا توجد بنود منهج في الشعب بعد. أضف المراحل والشعب والمنهج في الخطوات السابقة.",
  loadFailed: "تعذّر تحميل الأنصبة. أعد المحاولة.",
  filters: "تصفية",
  stageFilter: "المرحلة",
  allStages: "كل المراحل",
  teacherFilter: "المعلم",
  allTeachers: "كل المعلمين",
  unassigned: "بدون معلم",
  showAll: "عرض كل المعلمين في القوائم (لا المتخصصين فقط)",
  table: "إسناد المعلمين",
  section: "الشعبة",
  subject: "المادة",
  lessons: "حصص أسبوعياً",
  teacher: "المعلم",
  status: "الحالة",
  chooseTeacher: (subject: string, section: string) => `معلم ${isolate(subject)} في ${isolate(section)}`,
  noTeacher: "— بدون معلم —",
  teacherOption: (name: string, assigned: string, limit: string) => `${isolate(name)} (${assigned} من ${limit})`,
  sectionName: (stage: string, label: string) => `${isolate(stage)} / ${isolate(label)}`,
  statuses: {
    assigned: "مُسند",
    missing: "بلا معلم",
    overloaded: "المعلم فوق حده",
  },
  outside: "خارج التخصص",
  saved: "تم حفظ الإسناد.",
  suggestTitle: "اقتراح تلقائي للبنود التي بلا معلم",
  suggestHint: "يقترح معلمين للبنود الفارغة فقط ولا يغيّر أي إسناد موجود.",
  loadsTitle: "أنصبة المعلمين",
  loadColumn: "المسند من الحد",
  loadValue: (assigned: string, limit: string) => `${assigned} من ${limit}`,
  noTeachers: "لا يوجد معلمون بعد. أضفهم في خطوة المعلمين.",
} as const;
