# ADR 0036: The official study plan 2026-2027 replaces the suggested curriculum

- Status: Accepted (owner instruction, 2026-10-06). Supersedes the data-source parts of ADR 0028.
- Date: 2026-10-06

## Context
The owner supplied the Ministry of Education's official study plans for 2026-2027 (regulation 22 of 2011), transcribed from official images into `iraq-curriculum.official-2026-2027.json` (template version 2). It replaces `iraq-curriculum.suggested.json`, the unverified secondary source of ADR 0028.

## Decision
- **Embedded data, unchanged:** `src/SmartSchoolTimetable.Application/Templates/iraq-curriculum.official-2026-2027.json`, exactly as supplied. The loader (`SuggestedCurriculumTemplate`) accepts only `templateVersion: 2`.
- **New fields:**
  - `provenance` is an object (`source`, `status: official`, `transcribedBy`).
  - `entries[].optional`, `entries[].inStatedTotal` and `entries[].note`.
  - `stages[].verificationNote`, `totalMatchesPrinted` and `needsReview` (taken from the file).
- **Optional subjects, all unticked by default:**
  - اللغة الكردية counts in the official total.
  - اللغة الفرنسية, الحاسوب and منهج جرائم حزب البعث are added on top of it.
  - An unticked optional subject is never created.
- **Totals:** the preview shows the printed total («المجموع الرسمي») and the sum of the enabled rows («المحسوب للمواد المفعّلة»).
  - The rows counted in the official total (mandatory rows plus Kurdish) equal the printed total in every stage except الرابع الابتدائي (31 against 30). That stage is flagged `needsReview` with the source's note.
  - A difference is explained, never blocking.
- **Notes are data:** `note` and `verificationNote` are Arabic text from the template and are shown as-is. The panel's fixed source line comes from the dictionary: «المصدر: الخطة الدراسية الرسمية 2026-2027 (وزارة التربية). راجع الأرقام قبل الاعتماد.»
- **Subject names:** the template's aliases fold the plan's spellings into one canonical subject. Examples: «اللغة العربية (قراءتي)» → «اللغة العربية», «التربية الفنية» → «التربية الفنية والنشيد», «مبادئ الاقتصاد» → «الاقتصاد». Subjects are created under the canonical name, and existing subjects are reused.
- **Unchanged behaviour (ADR 0028/0029):**
  - Preview first, then an idempotent, non-destructive apply.
  - Values are marked «مقترح» until edited, and the per-stage reset needs confirmation.
  - The API routes (`/curriculum/suggested…`) and class names keep their names to avoid churn.
- **API shape:** `provenance` becomes an object, and the plan gains `templateVersion`.
  - Subject lines gain `inStatedTotal` and `note`.
  - Stage lines gain `officialTotal` and `verificationNote`.
  - Entry lines gain `inStatedTotal` and `note`.

## Consequences
- Open questions for the owner are in DECISIONS_PENDING #66–#68.
- How to change: replace the JSON (bump `templateVersion` and the loader check), `SuggestedCurriculumTemplate`, `SuggestedCurriculumService`, `SuggestedCurriculumPanel`.

## Addendum (2026-10-09): owner decisions #66–#68
- The JSON was replaced by the owner's update (same name, `templateVersion` still 2). Every stage's counted rows now equal the printed total (30/30/30/30/30/31, 30/30/30, 30/30/33, 30/31/31), and no stage has `needsReview`.
- الرابع الابتدائي is 30: الاجتماعيات is 2 lessons, with a note that the plan mentions a practical lesson and the school may edit it.
- منهج جرائم حزب البعث is in الخامس العلمي and الخامس الأدبي only (optional, one lesson, on top of the official total).
- Support for `needsReview` and `verificationNote` stays in the code and the panel for any future note.
- Break lengths and lessons per day are the owner's choice; `presets.json` values are suggestions («مقترح»).
