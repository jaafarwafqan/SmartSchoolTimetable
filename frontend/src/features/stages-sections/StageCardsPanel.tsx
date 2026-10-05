import { Trash2 } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { Stepper } from "../../components/ui/stepper";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSetSectionCount, useStageCards, type LabelStyle, type StageCard } from "../curriculum/curriculumApi";
import { useShifts, type Shift } from "../timetable-structure/scheduleApi";

const text = messages.school.stageCards;
const maxSections = 30;
export const labelStyles: readonly LabelStyle[] = ["arabic", "numbers", "latin"];

/** Shift and label-style choices for sections added by the stepper or a template. */
export function NewSectionOptions({ idPrefix, shifts, shiftId, style, onShift, onStyle }: {
  idPrefix: string;
  shifts: Shift[];
  shiftId: number | null;
  style: LabelStyle;
  onShift: (id: number) => void;
  onStyle: (style: LabelStyle) => void;
}) {
  return (
    <div className="form-grid">
      {shifts.length > 1 && (
        <Field id={`${idPrefix}-shift`} label={text.newSectionsShift}>
          <Select id={`${idPrefix}-shift`} value={String(shiftId ?? "")} onChange={(event) => onShift(Number(event.target.value))}
            options={shifts.map((shift) => ({ value: String(shift.id), label: shift.name }))} />
        </Field>
      )}
      <Field id={`${idPrefix}-style`} label={text.labelStyle}>
        <Select id={`${idPrefix}-style`} value={style} onChange={(event) => onStyle(event.target.value as LabelStyle)}
          options={labelStyles.map((value) => ({ value, label: text.labelStyles[value] }))} />
      </Field>
    </div>
  );
}

/**
 * Stage cards (spec 2.5 §3.4, §6): every stage with a section stepper and its section chips. Adding names the
 * new sections automatically; removing always takes the LAST section and is confirmed first.
 */
export function StageCardsPanel({ yearId }: { yearId: number }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const cards = useStageCards(yearId);
  const shifts = useShifts(yearId);
  const setCount = useSetSectionCount(yearId);
  const [shiftChoice, setShiftChoice] = useState<number | null>(null);
  const [style, setStyle] = useState<LabelStyle>("arabic");
  const [removing, setRemoving] = useState<StageCard | null>(null);
  const shiftList = shifts.data?.items ?? [];
  const shiftId = shiftList.find((shift) => shift.id === shiftChoice)?.id ?? shiftList[0]?.id ?? null;
  const shiftName = (id: number) => shiftList.find((shift) => shift.id === id)?.name ?? "";
  const items = cards.data ?? [];

  function submit(card: StageCard, count: number) {
    feedback.reset();
    setCount.mutate({ stageId: card.stage.id, count, shiftId: shiftId ?? 0, labelStyle: style }, {
      onSuccess: (saved) => feedback.showSuccess(text.updated(saved.stage.name, format.number(saved.sections.length))),
      onError: feedback.showError,
      onSettled: () => setRemoving(null),
    });
  }

  if (items.length === 0) return null;
  return (
    <Card className="page-card" aria-labelledby="stage-cards-title">
      <h2 id="stage-cards-title">{text.title}</h2>
      <p className="card-note">{text.description}</p>
      {cards.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {shifts.isSuccess && shiftList.length === 0 && <Alert tone="warning" message={text.noShift} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      {feedback.conflict && <Alert tone="error" message={messages.errors.CONFLICT} />}
      <NewSectionOptions idPrefix="cards" shifts={shiftList} shiftId={shiftId} style={style} onShift={setShiftChoice} onStyle={setStyle} />
      <ul className="stage-cards">
        {items.map((card) => {
          const capacities = [...new Map(card.sections.map((section) => [section.shiftId, section.weeklyCapacity])).entries()];
          return (
            <li key={`stage-card-${card.stage.id}`} className="stage-card">
              <div className="stage-card-header">
                <h3>{card.stage.name}</h3>
                <Stepper
                  id={`stage-card-count-${card.stage.id}`}
                  label={text.sectionCount(card.stage.name)}
                  value={card.sections.length}
                  min={0}
                  max={maxSections}
                  format={format.number}
                  decreaseLabel={text.decrease(card.stage.name)}
                  increaseLabel={text.increase(card.stage.name)}
                  disabled={setCount.isPending || (shiftId === null && card.sections.length === 0)}
                  onChange={(count) => (count < card.sections.length ? setRemoving(card) : submit(card, count))}
                />
              </div>
              {card.sections.length === 0 ? (
                <p className="stage-card-empty">{text.noSections}</p>
              ) : (
                <ul className="section-chips" aria-label={text.sectionsList(card.stage.name)}>
                  {card.sections.map((section) => (
                    <li key={`section-chip-${section.id}`} className="section-chip">
                      <bdi>{section.label}</bdi>
                      {shiftList.length > 1 && <span className="section-chip-shift">{shiftName(section.shiftId)}</span>}
                    </li>
                  ))}
                </ul>
              )}
              {capacities.map(([id, capacity]) => (
                <p key={`capacity-${card.stage.id}-${id}`} className="stage-card-capacity">{text.capacity(format.count(capacity, "lesson"), shiftName(id))}</p>
              ))}
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={removing !== null}
        danger
        title={text.removeTitle}
        consequence={removing ? text.removeConsequence(removing.sections.at(-1)?.label ?? "", removing.stage.name) : ""}
        confirmLabel={text.remove}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={setCount.isPending}
        onCancel={() => setRemoving(null)}
        onConfirm={() => removing && submit(removing, removing.sections.length - 1)}
      />
    </Card>
  );
}
