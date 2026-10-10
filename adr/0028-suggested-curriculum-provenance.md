# ADR 0028: The suggested Iraqi curriculum is an unverified, editable suggestion

- Status: Accepted (owner instruction, 2026-10-05); the data source is superseded by ADR 0036 (official plan 2026-2027)
- Date: 2026-10-05

## Context
The owner supplied weekly lessons per subject for Iraqi primary, intermediate and preparatory (scientific and literary) stages, taken from a secondary source. It was not verified against an official Ministry document.

## Decision
- **Stored as embedded data, unchanged:** `src/SmartSchoolTimetable.Application/Templates/iraq-curriculum.suggested.json`, exactly as supplied.
  - Version, provenance, subject aliases, 15 stages with rows, optional flags, stated and computed totals, `needsReview`, notes.
  - Tests validate it: stage names match the stage templates, lessons are 1–15, and the computed totals match the owner's table.
- **Honest labelling:** everything it fills is «مقترح» and editable. The panel shows «مقترح من خطة دراسية قدّمها المالك وغير مُتحقَّق منه رسمياً، راجعه مع خطة وزارتك». The product and docs never call it official.
- **Totals come from the rows, never from the stated totals.** Three literary stages (الرابع/الخامس/السادس الأدبي) state totals their rows do not reach. Their rows are kept as supplied, flagged `needsReview`, and shown with «مجموع الحصص المقترح لا يطابق المجموع المذكور في المصدر، يرجى المراجعة».
- **Optional subjects** (اللغة الكردية in the fourth and fifth preparatory grades; اللغة الفرنسية in all intermediate grades, an assumption) are unchecked by default.
- **Subject names:**
  - The canonical spelling follows the owner's list (اللغة الإنكليزية).
  - Existing subjects are matched through `subjectAliases` plus Arabic normalization (اللغة الإنجليزية, الجغرافيا, …) and never duplicated.
- **Scope:** only the school's existing stages are considered, matched by template key or stage name. A ثانوية uses only the grades and branches it chose.

## Consequences
- Updating the numbers later means replacing the JSON (bump `templateVersion`) after the owner checks them against the Ministry plan.
- How to change: `SuggestedCurriculumTemplate`, `SuggestedCurriculumService`.
