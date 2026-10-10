import { describe, expect, it } from "vitest";
import { messages } from "../../i18n/messages";
import { createFormatter } from "../../lib/format";
import { isolate } from "../../i18n/isolate";
import { weekdayLabel } from "../timetable-structure/weekdays";
import { describeChange, marksForSection } from "./comparison";
import type { Lookups } from "./TimetableGrids";
import type { LessonChange } from "./timetableApi";

const format = createFormatter();
const text = messages.school.lifecycle;

const look: Lookups = {
  subject: (id) => (id === 100 ? { name: "الرياضيات", colorIndex: 1 } : undefined),
  teacher: (id) => ({ name: id === 7 ? "أحمد" : "سعد", shortName: id === 7 ? "أحمد" : "سعد" }),
  section: () => undefined,
  sectionName: () => "",
};

const change = (overrides: Partial<LessonChange>): LessonChange => ({
  kind: "moved", sectionId: 1, lineId: 10, subjectId: 100, fromTeacherId: 7, toTeacherId: 7,
  fromDay: 1, fromLesson: 1, toDay: 2, toLesson: 3, ...overrides,
});

describe("comparison grid marks", () => {
  it("marks the new slot of a move and the vacated slot as «was»", () => {
    const marks = marksForSection([change({})], 1, new Set(["2:3"]));
    expect(marks.get("2:3")?.state).toBe("moved");
    expect(marks.get("1:1")?.state).toBe("was");
    expect(marks.size).toBe(2);
  });

  it("does not mark a vacated slot that another lesson now occupies", () => {
    const marks = marksForSection([change({})], 1, new Set(["2:3", "1:1"]));
    expect([...marks.keys()]).toEqual(["2:3"]);
  });

  it("marks added, removed and reassigned lessons and ignores other sections", () => {
    const changes = [
      change({ kind: "added", fromTeacherId: null, fromDay: null, fromLesson: null, toDay: 3, toLesson: 1 }),
      change({ kind: "removed", toTeacherId: null, toDay: null, toLesson: null, fromDay: 4, fromLesson: 2 }),
      change({ kind: "reassigned", toTeacherId: 9, fromDay: 5, fromLesson: 1, toDay: 5, toLesson: 1 }),
      change({ sectionId: 2, toDay: 1, toLesson: 6 }),
    ];
    const marks = marksForSection(changes, 1, new Set(["3:1", "5:1"]));
    expect(Object.fromEntries([...marks].map(([slot, mark]) => [slot, mark.state]))).toEqual({ "3:1": "added", "4:2": "was", "5:1": "reassigned" });
  });
});

describe("change sentences", () => {
  it("names the subject and both slots of a move", () => {
    expect(describeChange(change({}), format, look)).toBe(text.lineMoved(isolate("الرياضيات"), messages.school.timetable.slotLabel(weekdayLabel(1), "١"), messages.school.timetable.slotLabel(weekdayLabel(2), "٣")));
  });

  it("mentions the teachers when a move also changes the teacher", () => {
    const sentence = describeChange(change({ toTeacherId: 8 }), format, look);
    expect(sentence).toContain("أحمد");
    expect(sentence).toContain("سعد");
  });

  it("reads added and removed lessons", () => {
    expect(describeChange(change({ kind: "removed", toDay: null, toLesson: null, toTeacherId: null }), format, look)).toContain("حُذفت");
    expect(describeChange(change({ kind: "added", fromDay: null, fromLesson: null, fromTeacherId: null }), format, look)).toContain("أُضيفت");
  });
});
