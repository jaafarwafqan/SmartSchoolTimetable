import type { GridLesson } from "./timetableApi";

export type Slot = { sectionId: number; day: number; lesson: number };

/**
 * Moves the lesson at `from` to `to` in the same section: into an empty slot, or swapping with the lesson there.
 * Pure: returns a new list (the editor keeps every step for undo and redo).
 */
export function moveOrSwap(lessons: readonly GridLesson[], from: Slot, to: Slot): GridLesson[] {
  if (from.sectionId !== to.sectionId || (from.day === to.day && from.lesson === to.lesson)) return [...lessons];
  const at = (slot: Slot) => (lesson: GridLesson) => lesson.sectionId === slot.sectionId && lesson.day === slot.day && lesson.lesson === slot.lesson;
  const source = lessons.find(at(from));
  if (!source) return [...lessons];
  const target = lessons.find(at(to));
  return lessons.map((lesson) => {
    if (lesson === source) return { ...lesson, day: to.day, lesson: to.lesson };
    if (target && lesson === target) return { ...lesson, day: from.day, lesson: from.lesson };
    return lesson;
  });
}

/** Undo/redo history of whole timetables (session only). */
export type History = { past: GridLesson[][]; present: GridLesson[]; future: GridLesson[][] };

export function startHistory(lessons: readonly GridLesson[]): History {
  return { past: [], present: [...lessons], future: [] };
}

export function push(history: History, next: GridLesson[]): History {
  return { past: [...history.past, history.present], present: next, future: [] };
}

export function undo(history: History): History {
  const previous = history.past.at(-1);
  return previous ? { past: history.past.slice(0, -1), present: previous, future: [history.present, ...history.future] } : history;
}

export function redo(history: History): History {
  const next = history.future[0];
  return next ? { past: [...history.past, history.present], present: next, future: history.future.slice(1) } : history;
}

/** Lessons whose slot differs from the original version. */
export function changedCount(original: readonly GridLesson[], current: readonly GridLesson[]): number {
  const key = (lesson: GridLesson) => `${lesson.sectionId}:${lesson.lineId}:${lesson.day}:${lesson.lesson}`;
  const before = new Map<string, number>();
  for (const lesson of original) before.set(key(lesson), (before.get(key(lesson)) ?? 0) + 1);
  let changed = 0;
  for (const lesson of current) {
    const count = before.get(key(lesson)) ?? 0;
    if (count > 0) before.set(key(lesson), count - 1);
    else changed++;
  }
  return changed;
}
