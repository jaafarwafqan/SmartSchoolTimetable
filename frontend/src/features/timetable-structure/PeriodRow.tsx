import { CircleAlert, Trash2 } from "lucide-react";
import { Checkbox } from "../../components/ui/checkbox";
import { IconButton } from "../../components/ui/icon-button";
import { TimeField } from "../../components/TimeField";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import type { PeriodField } from "./periodRows";
import type { PeriodInput, PeriodKind } from "./scheduleApi";

const text = messages.school.scheduleStructure;
const kindOptions = [{ value: "lesson", label: text.lesson }, { value: "break", label: text.break }] as const;

type PeriodRowProps = {
  index: number;
  row: PeriodInput;
  lessonNumber: number | null;
  errors?: Partial<Record<PeriodField, string>>;
  format: Formatter;
  onChange: (row: PeriodInput) => void;
  onRemove: () => void;
};

/** One editable row of a shift's daily schedule; errors appear under the row with an icon. */
export function PeriodRow({ index, row, lessonNumber, errors = {}, format, onChange, onRemove }: PeriodRowProps) {
  const label = text.rowLabel(format.number(index + 1));
  const errorId = `period-${index}-error`;
  const messagesForRow = [errors.kind, errors.startTime, errors.endTime].filter((message): message is string => Boolean(message));
  const invalid = (field: PeriodField) => (errors[field] ? true : undefined);
  const lesson = row.kind === "lesson";
  return (
    <li className="period-row" aria-label={label}>
      <span className="period-row-number">{lessonNumber === null ? text.break : text.lessonNumber(format.number(lessonNumber))}</span>
      <Select
        aria-label={`${text.kind} - ${label}`}
        value={row.kind}
        options={kindOptions}
        aria-invalid={invalid("kind")}
        onChange={(event) => {
          const kind = event.target.value as PeriodKind;
          onChange({ ...row, kind, startBell: kind === "lesson", endBell: kind === "lesson" });
        }}
      />
      <TimeField compact id={`period-${index}-start`} label={`${text.startTime} - ${label}`} value={row.startTime}
        invalid={Boolean(invalid("startTime"))} onChange={(startTime) => onChange({ ...row, startTime })} />
      <TimeField compact id={`period-${index}-end`} label={`${text.endTime} - ${label}`} value={row.endTime}
        invalid={Boolean(invalid("endTime"))} onChange={(endTime) => onChange({ ...row, endTime })} />
      <Checkbox checked={lesson && row.startBell} disabled={!lesson} onChange={(event) => onChange({ ...row, startBell: event.target.checked })}>{text.startBell}</Checkbox>
      <Checkbox checked={lesson && row.endBell} disabled={!lesson} onChange={(event) => onChange({ ...row, endBell: event.target.checked })}>{text.endBell}</Checkbox>
      <IconButton aria-label={text.removeRow(format.number(index + 1))} title={text.removeRow(format.number(index + 1))} icon={<Trash2 aria-hidden="true" size={16} />} onClick={onRemove} />
      {messagesForRow.length > 0 && (
        <p id={errorId} className="ui-field-error period-row-error">
          <CircleAlert aria-hidden="true" size={16} />
          <span>{messagesForRow.join(" ")}</span>
        </p>
      )}
    </li>
  );
}
