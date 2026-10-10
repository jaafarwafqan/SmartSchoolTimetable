import { ArrowRightLeft, Ban, CircleAlert, History, Plus, Replace, type LucideIcon } from "lucide-react";

/** The last four show a version comparison (M1): moved to / added in / teacher changed in this cell, and where a lesson used to be. */
export type TimetableCellState = "normal" | "selected" | "conflict" | "blocked" | "drag-valid" | "drag-invalid" | "moved" | "added" | "reassigned" | "was";
export type SubjectColorIndex = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10;

// Literal class names so Tailwind generates them from the subject palette tokens.
export const subjectColorClasses: Record<SubjectColorIndex, string> = {
  1: "bg-subject-1",
  2: "bg-subject-2",
  3: "bg-subject-3",
  4: "bg-subject-4",
  5: "bg-subject-5",
  6: "bg-subject-6",
  7: "bg-subject-7",
  8: "bg-subject-8",
  9: "bg-subject-9",
  10: "bg-subject-10",
};

const comparisonIcons: Partial<Record<TimetableCellState, LucideIcon>> = { moved: ArrowRightLeft, added: Plus, reassigned: Replace, was: History };

type TimetableCellProps = {
  subject?: string;
  teacher?: string;
  color?: SubjectColorIndex;
  state?: TimetableCellState;
  /** Text for screen readers and the tooltip; required for conflict and blocked states. */
  description: string;
};

/**
 * DESIGN_SYSTEM.md 6.5 presentational cell: subject colour background with on-subject text.
 * Conflict = danger outline + CircleAlert; blocked = hatch + Ban; selected = primary outline;
 * drag targets = dashed success/danger outline; comparison cells carry an icon (moved, added, teacher changed, was here).
 * No state is conveyed by colour alone.
 */
export function TimetableCell({ subject, teacher, color, state = "normal", description }: TimetableCellProps) {
  const filled = state !== "blocked" && subject;
  const ComparisonIcon = comparisonIcons[state];
  // Empty cells get the surface background from CSS; filled cells get the subject utility class
  // (unlayered CSS would override Tailwind utilities, so the base class sets no background).
  const colorClass = filled && color ? subjectColorClasses[color] : state === "blocked" ? "" : "is-empty";
  const classes = ["ui-tt-cell", `is-${state}`, colorClass].filter(Boolean).join(" ");
  return (
    <div className={classes} role="group" tabIndex={0} aria-label={description} title={description}>
      {state === "blocked" && <Ban className="ui-tt-icon" aria-hidden="true" size={20} strokeWidth={2} />}
      {filled && (
        <>
          <span className="ui-tt-subject">{subject}</span>
          {teacher && <span className="ui-tt-teacher">{teacher}</span>}
        </>
      )}
      {ComparisonIcon && <ComparisonIcon className="ui-tt-icon ui-tt-change-icon" aria-hidden="true" size={16} strokeWidth={2} />}
      {state === "conflict" && <CircleAlert className="ui-tt-icon ui-tt-conflict-icon" aria-hidden="true" size={16} strokeWidth={2} />}
    </div>
  );
}
