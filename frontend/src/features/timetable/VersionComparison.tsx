import { ArrowRightLeft, CircleCheck, GitCompareArrows, History, ListChecks, Minus, Plus, Replace, Users, X } from "lucide-react";
import { useMemo, useState } from "react";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Field } from "../../components/ui/field";
import { SectionTitle } from "../../components/ui/section-title";
import { Select } from "../../components/ui/select";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { describeChange, marksForSection, type SlotMark } from "./comparison";
import { useComparison, useTimetable, type Comparison, type GridLesson, type Timetable, type TimetableVersionSummary } from "./timetableApi";
import { cellFor, lookups, WeekGrid, type Lookups } from "./TimetableGrids";

const text = messages.school.lifecycle;
const timetableText = messages.school.timetable;

/** Names from the newer version first, then from the older one (a removed lesson may name a teacher or section only it had). */
function mergedLookups(newer: Timetable, older: Timetable | undefined): Lookups {
  const first = lookups(newer);
  const second = older ? lookups(older) : undefined;
  return {
    subject: (id) => first.subject(id) ?? second?.subject(id),
    teacher: (id) => first.teacher(id) ?? second?.teacher(id),
    section: (id) => first.section(id) ?? second?.section(id),
    sectionName: (id) => first.sectionName(id) || second?.sectionName(id) || "",
  };
}

type Props = {
  /** The newer version, already loaded by the page. */
  target: Timetable;
  versions: TimetableVersionSummary[];
  baseId: number;
  onBaseChange: (id: number) => void;
  onClose: () => void;
};

/**
 * M1: what changed between two versions, in words, per section and per teacher, and as the newer grid with the changed
 * lessons marked (icon and outline, never colour alone) and the slots lessons left shown as «كانت هنا».
 */
export function VersionComparison({ target, versions, baseId, onBaseChange, onClose }: Props) {
  const format = useFormatter();
  const comparison = useComparison(baseId, target.summary.id);
  const base = useTimetable(baseId);
  const look = useMemo(() => mergedLookups(target, base.data), [target, base.data]);
  const others = versions.filter((version) => version.id !== target.summary.id);
  const data = comparison.data;
  return (
    <Card className="page-card version-comparison" aria-labelledby="version-comparison-title">
      <div className="generation-heading">
        <SectionTitle level={2} icon={GitCompareArrows} id="version-comparison-title">{text.compareTitle}</SectionTitle>
        <Button variant="secondary" icon={<X aria-hidden="true" size={18} />} onClick={onClose}>{text.compareClose}</Button>
      </div>
      <Field id="comparison-base" label={text.compareWithLabel} hint={text.compareHint(format.number(target.summary.number))}>
        <Select id="comparison-base" value={String(baseId)} onChange={(event) => onBaseChange(Number(event.target.value))}
          options={others.map((version) => ({ value: String(version.id), label: timetableText.version(format.number(version.number)) }))} />
      </Field>
      {comparison.isPending && <Spinner label={messages.school.common.loading} />}
      {comparison.isError && <Alert tone="error" message={text.compareLoadFailed}>{userErrorMessage(comparison.error)}</Alert>}
      {data && <ComparisonBody data={data} target={target} look={look} />}
    </Card>
  );
}

function ComparisonBody({ data, target, look }: { data: Comparison; target: Timetable; look: Lookups }) {
  const format = useFormatter();
  const totals = data.totals;
  const changedSections = data.sections.map((row) => row.sectionId);
  const [sectionChoice, setSectionChoice] = useState<number | null>(null);
  const sectionId = sectionChoice !== null && target.sections.some((item) => item.id === sectionChoice)
    ? sectionChoice
    : changedSections.find((id) => target.sections.some((item) => item.id === id)) ?? target.sections[0]?.id ?? 0;
  const section = target.sections.find((item) => item.id === sectionId);
  const sectionLessons = target.lessons.filter((lesson) => lesson.sectionId === sectionId);
  const occupied = useMemo(() => new Set(sectionLessons.map((lesson) => `${lesson.day}:${lesson.lesson}`)), [sectionLessons]);
  const marks = useMemo(() => marksForSection(data.changes, sectionId, occupied), [data.changes, sectionId, occupied]);
  const shifts = new Map(target.shifts.map((shift) => [shift.id, shift]));
  const lessonCount = Math.max(1, shifts.get(section?.shiftId ?? 0)?.lessons.length ?? 0, ...(section?.allowedByDay.map((day) => day.lessons) ?? [0]));

  if (totals.added + totals.removed + totals.moved + totals.reassigned === 0)
    return <p className="generation-verified"><CircleCheck aria-hidden="true" size={18} /><span>{text.compareIdentical}</span></p>;

  const count = (value: number) => format.number(value);
  const sectionColumns: TableColumn<Comparison["sections"][number]>[] = [
    { key: "section", header: text.sectionColumn, cell: (row) => look.sectionName(row.sectionId) },
    { key: "moved", header: text.kinds.moved, numeric: true, cell: (row) => count(row.moved) },
    { key: "added", header: text.kinds.added, numeric: true, cell: (row) => count(row.added) },
    { key: "removed", header: text.kinds.removed, numeric: true, cell: (row) => count(row.removed) },
    { key: "reassigned", header: text.kinds.reassigned, numeric: true, cell: (row) => count(row.reassigned) },
  ];
  const teacherColumns: TableColumn<Comparison["teachers"][number]>[] = [
    { key: "teacher", header: text.teacherColumn, cell: (row) => look.teacher(row.teacherId)?.name ?? "" },
    { key: "gained", header: text.gainedColumn, numeric: true, cell: (row) => count(row.gained) },
    { key: "lost", header: text.lostColumn, numeric: true, cell: (row) => count(row.lost) },
    { key: "moved", header: text.movedColumn, numeric: true, cell: (row) => count(row.moved) },
  ];
  const sectionChanges = data.changes.filter((change) => change.sectionId === sectionId);
  const ghost = (mark: SlotMark): GridLesson => ({
    sectionId, lineId: mark.change.lineId, subjectId: mark.change.subjectId, teacherId: mark.change.fromTeacherId ?? 0,
    day: mark.change.fromDay ?? 0, lesson: mark.change.fromLesson ?? 0,
  });
  const teacherShort = (lesson: GridLesson) => look.teacher(lesson.teacherId)?.shortName ?? "";
  const describe = {
    moved: text.cellMoved, added: text.cellAdded, reassigned: text.cellReassigned, was: text.cellWas,
  } as const;

  return (
    <>
      <div className="comparison-totals">
        <Badge tone="primary" icon={<ArrowRightLeft aria-hidden="true" size={16} />}>{text.kindCount(text.kinds.moved, count(totals.moved))}</Badge>
        <Badge tone="success" icon={<Plus aria-hidden="true" size={16} />}>{text.kindCount(text.kinds.added, count(totals.added))}</Badge>
        <Badge tone="danger" icon={<Minus aria-hidden="true" size={16} />}>{text.kindCount(text.kinds.removed, count(totals.removed))}</Badge>
        <Badge tone="warning" icon={<Replace aria-hidden="true" size={16} />}>{text.kindCount(text.kinds.reassigned, count(totals.reassigned))}</Badge>
        <Badge icon={<CircleCheck aria-hidden="true" size={16} />}>{text.kindCount(text.kinds.unchanged, count(totals.unchanged))}</Badge>
      </div>
      <p>{text.compareHeading(count(data.fromNumber), count(data.toNumber))}</p>
      <p className="card-note">{text.summary(count(totals.moved), count(totals.added), count(totals.removed), count(totals.reassigned), count(totals.unchanged))}</p>

      <section aria-labelledby="comparison-sections-title">
        <SectionTitle level={3} icon={ListChecks} id="comparison-sections-title">{text.perSection}</SectionTitle>
        <DataTable caption={text.perSection} columns={sectionColumns} rows={data.sections} rowKey={(row) => String(row.sectionId)} scrollable
          selectedKey={String(sectionId)} onSelect={(row) => setSectionChoice(row.sectionId)} />
      </section>
      <section aria-labelledby="comparison-teachers-title">
        <SectionTitle level={3} icon={Users} id="comparison-teachers-title">{text.perTeacher}</SectionTitle>
        <DataTable caption={text.perTeacher} columns={teacherColumns} rows={data.teachers} rowKey={(row) => String(row.teacherId)} scrollable />
      </section>

      {section && (
        <section aria-labelledby="comparison-grid-title">
          <SectionTitle level={3} icon={History} id="comparison-grid-title">{text.gridTitle(look.sectionName(section.id))}</SectionTitle>
          <ul className="comparison-legend" aria-label={text.legendTitle}>
            <li><ArrowRightLeft aria-hidden="true" size={16} /><span>{text.legendMoved}</span></li>
            <li><Plus aria-hidden="true" size={16} /><span>{text.legendAdded}</span></li>
            <li><Replace aria-hidden="true" size={16} /><span>{text.legendReassigned}</span></li>
            <li><History aria-hidden="true" size={16} /><span>{text.legendWas}</span></li>
          </ul>
          <WeekGrid caption={look.sectionName(section.id)} days={target.days} shift={shifts.get(section.shiftId)} lessonCount={lessonCount}
            format={format} look={look} lessons={sectionLessons} secondLine={teacherShort}
            cell={(lesson, day, number) => {
              const mark = marks.get(`${day}:${number}`);
              const shown = mark?.state === "was" ? ghost(mark) : lesson;
              const base = cellFor(shown, day, number, format, look, teacherShort);
              return mark ? { ...base, state: mark.state, description: describe[mark.state](base.description) } : base;
            }} />
          <SectionTitle level={3} icon={ListChecks}>{text.changeList}</SectionTitle>
          <ul className="comparison-changes">
            {sectionChanges.map((change, index) => (
              <li key={`${change.kind}-${change.lineId}-${index}`}>{describeChange(change, format, look)}</li>
            ))}
            {sectionChanges.length === 0 && <li>{text.noSectionChanges}</li>}
          </ul>
        </section>
      )}
    </>
  );
}
