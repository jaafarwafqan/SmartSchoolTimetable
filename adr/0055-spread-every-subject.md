# ADR 0055: Spread every subject across the days, and say which repeats are unavoidable

- Status: accepted (owner's analysis before the 1.0.0 tag)
- Date: 2026-10-17
- Related: ADR 0053 (generation rules), docs/SOLVER.md §3

## Context

The owner analysed a real timetable (11 sections, 30 lessons in 30 periods). The score showed «توزيع حصص المادة على أيام الأسبوع» with penalty 0
while 51 same-day repeats existed, 37 of them avoidable. Cause: S1 only counted subjects whose switch `SpreadAcrossDays` was on, and every subject
was created with it off (quick add, templates, suggested curriculum), so the rule never acted. Second, «تجنّب تكرار المادة» (S4) counts only
back-to-back repeats; one of its labels («تجنب تكرار المادة متتالية») could be read as any repeat in the day.

## Decision

1. S1 applies to every subject by default. New subjects are created with the switch on (quick add, subject templates, suggested curriculum);
   the corrective migration `Phase5SpreadAllSubjects` turns it on for every existing subject. The switch stays on the subject card for exceptions.
2. The penalty, per (section, subject, day) with `c` lessons: `max(0, c − base) + max(0, c − share)`, where `base` is 1 (2 for a double subject,
   one sitting) and `share = max(base, ⌈weekly ÷ days⌉)`, `days` being the section's working days on which the subject can be taught and one of its
   teachers is there (not off, not released, not blocked all day). Each same-day repeat costs one; a pile-up above the fair share costs one more.
   A single definition (`SubjectSpread`) is read by the CP-SAT model and the scorer, so the score still equals the objective (tested).
3. Default weight 40 («مهم جداً»). The migration lifts a saved 20 (the old default) to 40 and keeps any other weight the owner chose.
4. The result notes count the same-day repeats in plain Arabic and split them: per (section, subject), up to `max(0, weekly − base × days)` are
   «لا مفرّ منها» and the rest «يمكن تحسينها». The unavoidable ones are listed per teacher and subject with the reason: the teacher is not there on
   some of the section's days, or the subject has more weekly lessons than days. When every section fills every period of its week, the last-period
   note explains that each section then has a last lesson every day (sections × days of them a week).
5. S4 keeps its meaning (back-to-back only) and every label says so: «تجنّب تكرار المادة في حصتين متتاليتين»; its hint points to S1 for repeats in
   the day.

## Consequences

- Measured (synthetic, owner-shaped, 11 sections, 2 workers, every priority at its default): with S1 effectively off the solver left 31–55
  improvable repeats; with the fix 5–8 at 30 s and 2–7 at 60 s (runs vary), the 30–37 unavoidable ones unchanged; with S1 alone, 0 in 30 s.
  A weight of 120 gave 0–2 at 60 s but slightly more adjacent teacher lessons and up to 3 runs over the consecutive limit. The default stays 40,
  the top of the owner's three-level scale; a heavier default is an open owner decision (DECISIONS_PENDING #105). The first-timetable gate is
  unchanged (1–3 s, docs/PERFORMANCE.md).
- `SchedulingInput` is unchanged (the switch was already part of it), so the hash of a school changes only through the migrated switches, which
  marks old timetables as made with different data, as it should.
- The migration cannot know which subjects had the switch off on purpose; the owner turns it off again on those subjects' cards.
