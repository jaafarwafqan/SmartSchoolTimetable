import { BadgeCheck, FileDown, PencilLine, Play, Printer, ShieldCheck, Stamp } from "lucide-react";
import { useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ChipGroup } from "../../components/ui/chip-group";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useApproveTimetable, useTimetable, useTimetableVersions, type Term, type Timetable, type TimetableVersionSummary } from "./timetableApi";
import { lookups, MasterGrid, sessionViewOf, WeekGrid } from "./TimetableGrids";
import { TimetableEditor } from "./TimetableEditor";

const text = messages.school.timetable;

type View = "section" | "teacher" | "master";

function TimetableViews({ timetable, onSaved }: { timetable: Timetable; onSaved: (versionId: number) => void }) {
  const format = useFormatter();
  const look = useMemo(() => lookups(timetable), [timetable]);
  const [view, setView] = useState<View>("section");
  const [sectionId, setSectionId] = useState<number>(timetable.sections[0]?.id ?? 0);
  const [teacherId, setTeacherId] = useState<number>(timetable.teachers[0]?.id ?? 0);
  const [editing, setEditing] = useState(false);
  // R3: two-session schools choose the semester; the grid is the same, the clock follows the day's session. It opens on
  // the current semester when the year's dates tell it, otherwise on semester 1 (#86).
  const [term, setTerm] = useState<Term>(timetable.sessions?.currentTerm ?? 1);
  const shifts = new Map(timetable.shifts.map((shift) => [shift.id, shift]));
  const section = timetable.sections.find((item) => item.id === sectionId) ?? timetable.sections[0];
  const teacher = timetable.teachers.find((item) => item.id === teacherId) ?? timetable.teachers[0];
  const teacherLessons = timetable.lessons.filter((lesson) => lesson.teacherId === teacher?.id);
  const teacherShifts = [...new Set(teacherLessons.map((lesson) => look.section(lesson.sectionId)?.shiftId ?? 0))];
  const lessonCountOf = (shiftId: number) => Math.max(1, shifts.get(shiftId)?.lessons.length ?? 0,
    ...timetable.sections.filter((item) => item.shiftId === shiftId).flatMap((item) => item.allowedByDay.map((day) => day.lessons)));
  const school = useSchoolContext();
  const viewTitle = view === "master" ? text.views.master : view === "teacher" ? teacher?.name ?? "" : section ? look.sectionName(section.id) : "";
  return (
    <div className={`timetable-views${view === "master" ? " is-landscape" : ""}`}>
      <div className="timetable-print-header">
        <strong>{school.data?.schoolName ?? ""}</strong>
        <span>{text.printYear(school.data?.currentYear?.name ?? "", school.data?.currentTerm?.name ?? "")}</span>
        <span>{text.printVersion(format.number(timetable.summary.number), viewTitle)}</span>
        {timetable.sessions && <span>{text.printSemester(text.terms[term])}</span>}
      </div>
      {timetable.sessions && (
        <div className="timetable-controls timetable-term">
          <ChipGroup label={text.termLabel} caption={text.termLabel} value={term} onChange={(value) => setTerm(value === 2 ? 2 : 1)}
            options={([1, 2] as const).map((value) => ({ value, label: text.terms[value] }))} />
          <span className="card-note">{text.termHint}</span>
        </div>
      )}
      {!editing && (
        <div className="timetable-controls">
          <Button variant="secondary" icon={<Printer aria-hidden="true" size={18} />} onClick={() => window.print()}>{text.print}</Button>
          <a className="link-button" href={`/api/v1/timetables/${timetable.summary.id}/export.xlsx${timetable.sessions ? `?term=${term}` : ""}`} download>
            <FileDown aria-hidden="true" size={18} /><span>{text.exportExcel}</span>
          </a>
          <span className="card-note">{text.printHint}</span>
        </div>
      )}
      <div className="timetable-controls">
        <Field id="timetable-view" label={text.viewsLabel}>
          <Select id="timetable-view" value={view} onChange={(event) => { setView(event.target.value as View); setEditing(false); }}
            options={(["section", "teacher", "master"] as const).map((value) => ({ value, label: text.views[value] }))} />
        </Field>
        {view === "section" && (
          <Field id="timetable-section" label={text.chooseSection}>
            <Select id="timetable-section" value={String(section?.id ?? "")} onChange={(event) => setSectionId(Number(event.target.value))}
              options={timetable.sections.map((item) => ({ value: String(item.id), label: look.sectionName(item.id) }))} />
          </Field>
        )}
        {view === "teacher" && (
          <Field id="timetable-teacher" label={text.chooseTeacher}>
            <Select id="timetable-teacher" value={String(teacher?.id ?? "")} onChange={(event) => setTeacherId(Number(event.target.value))}
              options={timetable.teachers.map((item) => ({ value: String(item.id), label: item.name }))} />
          </Field>
        )}
      </div>
      {view === "section" && section && !editing && (
        <div>
          <Button variant="secondary" icon={<PencilLine aria-hidden="true" size={18} />} onClick={() => setEditing(true)}>{text.edit}</Button>
        </div>
      )}
      {view === "section" && section && editing && (
        <TimetableEditor timetable={timetable} sectionId={section.id} format={format} look={look} lessonCount={lessonCountOf(section.shiftId)}
          sessions={sessionViewOf(timetable, section.shiftId, term)}
          onClose={() => setEditing(false)} onSaved={(id) => { setEditing(false); onSaved(id); }} />
      )}
      {view === "section" && section && !editing && (
        <WeekGrid caption={look.sectionName(section.id)} days={timetable.days} shift={shifts.get(section.shiftId)} format={format} look={look}
          lessonCount={lessonCountOf(section.shiftId)} lessons={timetable.lessons.filter((lesson) => lesson.sectionId === section.id)}
          sessions={sessionViewOf(timetable, section.shiftId, term)}
          secondLine={(lesson) => look.teacher(lesson.teacherId)?.shortName ?? ""} />
      )}
      {view === "teacher" && teacher && (
        <>
          <p className="card-note">{text.teacherLoad(format.count(teacherLessons.length, "lesson"))}</p>
          {teacherShifts.map((shiftId) => (
            <WeekGrid key={shiftId} caption={teacherShifts.length > 1 ? text.teacherInShift(teacher.name, shifts.get(shiftId)?.name ?? "") : teacher.name}
              days={timetable.days} shift={shifts.get(shiftId)} format={format} look={look} lessonCount={lessonCountOf(shiftId)}
              sessions={sessionViewOf(timetable, shiftId, term)}
              lessons={teacherLessons.filter((lesson) => look.section(lesson.sectionId)?.shiftId === shiftId)}
              secondLine={(lesson) => look.sectionName(lesson.sectionId)} />
          ))}
        </>
      )}
      {view === "master" && <MasterGrid timetable={timetable} format={format} look={look} term={term} />}
    </div>
  );
}

function VersionsTable({ versions, selectedId, onSelect }: { versions: TimetableVersionSummary[]; selectedId: number | undefined; onSelect: (id: number) => void }) {
  const format = useFormatter();
  const columns: TableColumn<TimetableVersionSummary>[] = [
    { key: "number", header: text.versionColumn, cell: (row) => text.version(format.number(row.number)) },
    { key: "source", header: text.sourceColumn, cell: (row) => text.sources[row.source] },
    { key: "date", header: text.createdAt, cell: (row) => format.date(row.createdAt.slice(0, 10)) },
    { key: "score", header: text.score, numeric: true, cell: (row) => (row.score === null ? messages.school.generation.none : format.number(row.score)) },
    {
      key: "approved", header: text.statusColumn,
      cell: (row) => (row.isApproved ? <Badge tone="success" icon={<BadgeCheck aria-hidden="true" size={16} />}>{text.approved}</Badge> : text.notApproved),
    },
  ];
  return (
    <DataTable caption={text.versions} columns={columns} rows={versions} rowKey={(row) => String(row.id)} scrollable
      selectedKey={selectedId === undefined ? null : String(selectedId)} onSelect={(row) => onSelect(row.id)} />
  );
}
export function TimetablePage() {
  const format = useFormatter();
  const school = useSchoolContext();
  const yearId = school.data?.currentYear?.id;
  const versions = useTimetableVersions(yearId);
  const [params, setParams] = useSearchParams();
  const requested = Number(params.get("version")) || undefined;
  const list = versions.data ?? [];
  const selectedId = requested ?? list.find((item) => item.isApproved)?.id ?? list[0]?.id;
  const timetable = useTimetable(selectedId);
  const approve = useApproveTimetable();
  const feedback = useFormFeedback();
  const [confirming, setConfirming] = useState(false);
  const summary = timetable.data?.summary;

  return (
    <div className="page timetable-print-root">
      <PageHeader title={text.title} description={text.description} />
      {!yearId && school.isSuccess && <Alert tone="warning" message={messages.school.readiness.noYear} />}
      {versions.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(versions.error)}</Alert>}
      <Alert tone="error" message={feedback.error} />
      <Alert tone="success" message={feedback.success} />
      {versions.data && list.length === 0 && (
        <Card className="page-card">
          <p>{text.noVersions}</p>
          <Link className="link-button" to="/timetable/generate"><Play aria-hidden="true" size={18} /><span>{text.goGenerate}</span></Link>
        </Card>
      )}
      {list.length > 0 && (
        <div className="timetable-layout">
          <Card className="page-card timetable-versions" aria-labelledby="timetable-versions-title">
            <h2 id="timetable-versions-title">{text.versions}</h2>
            <VersionsTable versions={list} selectedId={selectedId} onSelect={(id) => setParams({ version: String(id) })} />
          </Card>
          <Card className="page-card timetable-main" aria-labelledby="timetable-main-title">
            {timetable.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(timetable.error)}</Alert>}
            {summary && timetable.data && (
              <>
                <div className="generation-heading">
                  <h2 id="timetable-main-title">{text.version(format.number(summary.number))}</h2>
                  <div className="page-header-actions">
                    {summary.isApproved
                      ? <Badge tone="success" icon={<BadgeCheck aria-hidden="true" size={16} />}>{text.approved}</Badge>
                      : <Button icon={<Stamp aria-hidden="true" size={18} />} onClick={() => setConfirming(true)}>{text.approve}</Button>}
                  </div>
                </div>
                {timetable.data.violations === 0
                  ? <p className="generation-verified"><ShieldCheck aria-hidden="true" size={18} /><span>{text.verified}</span></p>
                  : <Alert tone="error" message={messages.errors.TIMETABLE_VERIFICATION_FAILED} />}
                {summary.stale && <Alert tone="warning" message={text.stale} />}
                <TimetableViews key={summary.id} timetable={timetable.data}
                  onSaved={(id) => { setParams({ version: String(id) }); feedback.showSuccess(text.savedEdit); }} />
              </>
            )}
            {!summary && timetable.isPending && <p>{messages.app.loading}</p>}
          </Card>
        </div>
      )}
      {summary && (
        <ConfirmDialog open={confirming} title={text.approveConfirmTitle} consequence={text.approveConfirm(format.number(summary.number))}
          confirmLabel={text.approve} confirmIcon={<Stamp aria-hidden="true" size={18} />} loading={approve.isPending}
          onCancel={() => setConfirming(false)}
          onConfirm={() => approve.mutate(summary, {
            onSuccess: () => { setConfirming(false); feedback.showSuccess(text.approvedDone); },
            onError: (error) => { setConfirming(false); feedback.showError(error); },
          })} />
      )}
      {versions.isPending && yearId && <p>{messages.app.loading}</p>}
    </div>
  );
}
