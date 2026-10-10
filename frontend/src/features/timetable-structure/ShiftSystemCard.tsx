import { ArrowRightLeft, Check, Clock, Moon, Sun, SunMoon } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ChoiceCards } from "../../components/ui/choice-cards";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useTemplateCatalog } from "../curriculum/curriculumApi";
import { useSchoolProfile } from "../school-profile/profileApi";
import { breakIssues } from "./BreaksEditor";
import { useShifts, useWorkingWeek } from "./scheduleApi";
import { useSessionPlan } from "./sessionPlanApi";
import { initialValue, toCommand, type ShiftSystem, type ShiftSystemValue } from "./shiftSystem";
import { useConvertLegacy, useSaveShiftSystem, useShiftSystem, type ShiftSystemState } from "./shiftSystemApi";
import { ShiftSystemEditor } from "./ShiftSystemEditor";

const text = messages.school.shiftSystem;

/** True when a value has a break the editor marks as wrong (blocks saving, as in the breaks editor). */
export function hasBreakIssues(value: ShiftSystemValue): boolean {
  const main = breakIssues(value.main.lessonCount, value.main.breaks).some((issue) => issue !== null);
  const evening = value.system === "dual" && breakIssues(value.main.lessonCount, value.evening.breaks).some((issue) => issue !== null);
  return main || evening;
}

/** The old two-shift layout: a one-time guided conversion (the server takes an automatic backup first). */
export function LegacyConversion({ state }: { state: ShiftSystemState }) {
  const format = useFormatter();
  const convert = useConvertLegacy();
  const feedback = useFormFeedback();
  const [target, setTarget] = useState<ShiftSystem>("dual");
  const [confirming, setConfirming] = useState(false);
  return (
    <div className="form-stack legacy-conversion">
      <Alert tone="warning" message={text.legacyTitle}>{text.legacyBody}</Alert>
      <ul className="reference-list">
        {state.legacyShifts.map((shift) => <li key={shift.id}>{text.legacyShift(shift.name, format.count(shift.sections, "section"))}</li>)}
      </ul>
      <ChoiceCards name="legacy-target" legend={text.legacyTarget} value={target} onChange={setTarget} choices={[
        { value: "morning", label: text.systems.morning, description: text.hints.morning, icon: <Sun aria-hidden="true" size={20} /> },
        { value: "evening", label: text.systems.evening, description: text.hints.evening, icon: <Moon aria-hidden="true" size={20} /> },
        { value: "dual", label: text.systems.dual, description: text.hints.dual, icon: <SunMoon aria-hidden="true" size={20} /> },
      ]} />
      <Alert tone="error" message={feedback.error} />
      <Alert tone="success" message={feedback.success} />
      <div><Button icon={<ArrowRightLeft aria-hidden="true" size={18} />} onClick={() => setConfirming(true)}>{text.convert}</Button></div>
      <ConfirmDialog open={confirming} title={text.convertConfirmTitle} consequence={text.convertConfirm(text.systems[target])}
        confirmLabel={text.convert} confirmIcon={<ArrowRightLeft aria-hidden="true" size={18} />} loading={convert.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => convert.mutate(target, {
          onSuccess: (result) => { setConfirming(false); feedback.showSuccess(text.converted(result.automaticBackupPath.split(/[\\/]/).pop() ?? "")); },
          onError: (error) => { setConfirming(false); feedback.showError(error); },
        })} />
    </div>
  );
}

/** «المدرسة › الدوام والحصص والجرس»: the same component as the wizard's timing step, saved on its own. */
/** `feedback` and `save` live in the card: a save remounts this form with the stored values, and the message (and the mutation callbacks) must survive it. */
function ShiftSystemForm({ start, days, feedback, save }: { start: ShiftSystemValue; days: number[]; feedback: ReturnType<typeof useFormFeedback>; save: ReturnType<typeof useSaveShiftSystem> }) {
  const catalog = useTemplateCatalog().data;
  const schoolType = useSchoolProfile().data?.schoolType ?? "other";
  const [value, setValue] = useState(start);
  return (
    <>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <ShiftSystemEditor idPrefix="school-shift" value={value} days={days} presets={catalog?.periodPresets ?? []}
        suggestedBreak={catalog?.breakDefaults.minutes[schoolType] ?? 15} onChange={(next) => { feedback.reset(); setValue(next); }} />
      <div className="form-actions">
        <Button icon={<Check aria-hidden="true" size={20} />} loading={save.isPending}
          onClick={() => {
            feedback.reset();
            if (hasBreakIssues(value)) {
              feedback.setError(text.blocked);
              return;
            }
            save.mutate(toCommand(value, days), { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError });
          }}>{text.save}</Button>
      </div>
    </>
  );
}

export function ShiftSystemCard() {
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id ?? null;
  const state = useShiftSystem();
  const shifts = useShifts(yearId);
  const plan = useSessionPlan();
  const week = useWorkingWeek();
  const feedback = useFormFeedback();
  const save = useSaveShiftSystem();
  const ready = state.data && week.data && (yearId === null || shifts.data) && plan.data;
  const shift = shifts.data?.items[0];
  return (
    <Card className="page-card" aria-labelledby="shift-system-title">
      <SectionTitle level={2} icon={Clock} id="shift-system-title">{text.title}</SectionTitle>
      <p className="ui-field-hint">{text.description}</p>
      {(state.isError || plan.isError) && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {!ready && !state.isError && <Spinner label={messages.school.common.loading} />}
      {yearId === null && state.data && <Alert tone="warning" message={messages.errors.NO_CURRENT_YEAR} />}
      {ready && state.data!.legacy && <LegacyConversion state={state.data!} />}
      {ready && yearId !== null && !state.data!.legacy && (
        <ShiftSystemForm key={`${state.data!.system}-${shift?.version ?? 0}-${plan.data!.version}`} days={week.data!.days} feedback={feedback} save={save}
          start={initialValue(state.data!.system, shift, plan.data, week.data!.days)} />
      )}
    </Card>
  );
}
