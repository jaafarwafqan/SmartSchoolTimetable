import { Eraser, SearchCheck } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useCleanOrphans, useOrphanBlockedPeriods, type OrphanOwner } from "./orphansApi";
import { weekdayLabel } from "./weekdays";

const text = messages.school.orphanBlocked;

function ownerName(owner: OrphanOwner): string {
  return owner.kind === "teacher" ? text.teacher(owner.name) : text.subject(owner.name);
}

/**
 * Blocked periods that no longer exist after the working days or lessons per day changed (Phase 3 §5.5).
 * Nothing is removed until the owner reviews the list and confirms.
 */
export function OrphanBlockedNotice() {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const orphans = useOrphanBlockedPeriods();
  const clean = useCleanOrphans();
  const [reviewing, setReviewing] = useState(false);
  const report = orphans.data;

  return (
    <>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {report && report.total > 0 && (
        <Alert tone="warning" message={text.notice(format.number(report.total))}>
          <span>
            <Button variant="secondary" icon={<SearchCheck aria-hidden="true" size={20} />} onClick={() => { feedback.reset(); setReviewing(true); }}>
              {text.review}
            </Button>
          </span>
        </Alert>
      )}
      <ConfirmDialog
        open={reviewing && !!report && report.total > 0}
        danger
        title={text.title}
        consequence={text.consequence}
        confirmLabel={text.remove}
        confirmIcon={<Eraser aria-hidden="true" size={20} />}
        loading={clean.isPending}
        onCancel={() => setReviewing(false)}
        onConfirm={() => report && clean.mutate(report.owners, {
          onSuccess: () => feedback.showSuccess(text.removed),
          onError: feedback.showError,
          onSettled: () => setReviewing(false),
        })}
      >
        <ul className="reference-list">
          {report?.owners.map((owner) => (
            <li key={`${owner.kind}-${owner.id}`}>
              {text.ownerSlots(ownerName(owner), owner.periods.map((period) => text.slot(weekdayLabel(period.day), format.number(period.lessonNumber))).join(text.slotSeparator))}
            </li>
          ))}
        </ul>
      </ConfirmDialog>
    </>
  );
}
