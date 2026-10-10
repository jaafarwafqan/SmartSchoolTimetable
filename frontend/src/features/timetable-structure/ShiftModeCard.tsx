import { SectionTitle } from "../../components/ui/section-title";
import { Moon, Sun, SunMoon, Check, Split } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ChoiceCards, type Choice } from "../../components/ui/choice-cards";
import { messages } from "../../i18n/messages";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSchoolProfile } from "../school-profile/profileApi";
import { useSetShiftMode, useShiftModeImpact, type ShiftMode } from "./shiftModeApi";

const text = messages.school.shiftMode;
const choices: readonly Choice<ShiftMode>[] = [
  { value: "morning", label: text.morning, description: text.morningHint, icon: <Sun size={20} /> },
  { value: "evening", label: text.evening, description: text.eveningHint, icon: <Moon size={20} /> },
  { value: "dual", label: text.dual, description: text.dualHint, icon: <SunMoon size={20} /> },
];
const isMode = (value: string | undefined): value is ShiftMode => value === "morning" || value === "evening" || value === "dual";

/** Shift mode (spec 2.5 §3.2): choose a card, see what changes, apply. Blocked while sections use a shift to remove. */
export function ShiftModeCard() {
  const feedback = useFormFeedback();
  const profile = useSchoolProfile();
  const current = isMode(profile.data?.studyType) ? profile.data.studyType : null;
  const [chosen, setChosen] = useState<ShiftMode | null>(null);
  const target = chosen ?? current;
  const impact = useShiftModeImpact(target);
  const apply = useSetShiftMode();
  const changes = impact.data;
  const unchanged = changes !== undefined && changes.shiftsToCreate.length === 0 && changes.shiftsToRemove.length === 0 && target === current;

  return (
    <Card className="page-card" aria-labelledby="shift-mode-title">
      <SectionTitle level={2} icon={Split} id="shift-mode-title">{text.title}</SectionTitle>
      <p className="ui-field-hint">{text.description}</p>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); void profile.refetch(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error ?? (impact.error ? userErrorMessage(impact.error) : null)} />
      <ChoiceCards name="shift-mode" legend={text.legend} choices={choices} value={target} onChange={(mode) => { feedback.reset(); setChosen(mode); }} />
      {changes && !unchanged && (
        <div className="shift-mode-impact" aria-live="polite">
          {changes.shiftsToCreate.map((kind) => <p key={kind}>{text.willCreate(kind === "morning" ? text.morningShift : text.eveningShift)}</p>)}
          {changes.shiftsToRemove.map((name) => <p key={name}>{text.willRemove(name)}</p>)}
          {!changes.allowed && (
            <Alert tone="warning" message={text.blocked}>
              <ul className="affected-list">
                {changes.affectedSections.map((section) => (
                  <li key={section.sectionId}>{text.affected(section.stageName, section.label, section.shiftName, section.isArchived)}</li>
                ))}
              </ul>
            </Alert>
          )}
        </div>
      )}
      <div className="form-actions">
        <Button icon={<Check aria-hidden="true" size={20} />} loading={apply.isPending}
          disabled={!target || !profile.data || !changes?.allowed || unchanged || feedback.conflict}
          onClick={() => target && profile.data && apply.mutate({ mode: target, version: profile.data.version }, {
            onSuccess: () => { setChosen(null); feedback.showSuccess(text.applied); },
            onError: feedback.showError,
          })}>
          {text.apply}
        </Button>
      </div>
    </Card>
  );
}
