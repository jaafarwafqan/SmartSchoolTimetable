import { SectionTitle } from "../../components/ui/section-title";
import { Archive, ArchiveRestore, BadgeCheck, CalendarCheck, CalendarRange, FileDown, FilePen, GitCompareArrows, History, PencilLine, Play, Printer, ShieldCheck, Stamp } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { flushSync } from "react-dom";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ChipGroup } from "../../components/ui/chip-group";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useStartRepair } from "../generation/generationApi";
import { useApproveTimetable, useArchiveTimetable, useCurrentCheck, useReplaceTeachers, useRollbackTimetable, useTimetable, useTimetableVersions, type Term, type Timetable, type TimetableVersionSummary } from "./timetableApi";
import { VersionComparison } from "./VersionComparison";
import { PrintDocument } from "./PrintDocument";
import { defaultOptions, type Orientation, type PaperSize, type PrintOptions, type PrintScope } from "./printing";
import { CurrentDataCheck } from "./CurrentDataCheck";
import { flaggedSlots, slotKey, type FindingItem } from "./currentCheck";
import { cellFor, lookups, MasterGrid, sessionViewOf, WeekGrid } from "./TimetableGrids";
import { TimetableEditor } from "./TimetableEditor";

const text = messages.school.timetable;
const lifecycle = messages.school.lifecycle;
const currentText = messages.school.currentCheck;

type View = "section" | "teacher" | "master";

type ViewsProps = {
  timetable: Timetable;
  /** MF4: the page owns the editing state, so it can hide «اعتماد» and the other version actions while editing. */
  editing: boolean;
  onEditingChange: (editing: boolean) => void;
  onSaved: (versionId: number) => void;
  /** MF11: slots ("section:day:lesson") that conflict with today's school data; marked on the grids outside the editor. */
  flagged: ReadonlySet<string>;
  /** MF11: «عرض في الجدول»: switch to the finding's section (or teacher) and focus its cell. `nonce` makes a repeated request count. */
  focusRequest: { nonce: number; item: FindingItem } | null;
};

function TimetableViews({ timetable, editing, onEditingChange, onSaved, flagged, focusRequest }: ViewsProps) {
  const format = useFormatter();
  const look = useMemo(() => lookups(timetable), [timetable]);
  const [chosenView, setView] = useState<View>("section");
  // The manual editor works on one section at a time, so editing always shows the section view.
  const view: View = editing ? "section" : chosenView;
  const setEditing = onEditingChange;
  const [sectionId, setSectionId] = useState<number>(timetable.sections[0]?.id ?? 0);
  const [teacherId, setTeacherId] = useState<number>(timetable.teachers[0]?.id ?? 0);
  // «عرض في الجدول»: adjust the view while rendering when a new request arrives (no effect needed for state).
  const [seenRequest, setSeenRequest] = useState(0);
  if (focusRequest && focusRequest.nonce !== seenRequest) {
    setSeenRequest(focusRequest.nonce);
    const { sectionId: wantedSection, teacherId: wantedTeacher } = focusRequest.item;
    if (wantedSection !== null && timetable.sections.some((item) => item.id === wantedSection)) {
      setView("section");
      setSectionId(wantedSection);
    } else if (wantedTeacher !== null && timetable.teachers.some((item) => item.id === wantedTeacher)) {
      setView("teacher");
      setTeacherId(wantedTeacher);
    }
  }
  useEffect(() => {
    if (!focusRequest) return undefined;
    const { day, lesson } = focusRequest.item;
    const handle = window.setTimeout(() => {
      const row = day === null ? -1 : timetable.days.indexOf(day);
      const exact = row >= 0 && lesson !== null ? document.querySelector<HTMLElement>(`.timetable-views [data-cell="${row}:${lesson - 1}"] .ui-tt-cell`) : null;
      const target = exact ?? document.querySelector<HTMLElement>(".timetable-views .ui-tt-cell.is-conflict");
      target?.scrollIntoView({ block: "center" });
      target?.focus();
    }, 60);
    return () => window.clearTimeout(handle);
  }, [focusRequest, timetable.days]);
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
  // MF5: the print job follows the owner's defaults for what is printed, and can be changed before printing.
  const [print, setPrint] = useState<PrintOptions>(() => defaultOptions("current", "section"));
  const chooseView = (next: View) => { setView(next); setEditing(false); setPrint((options) => defaultOptions(options.scope, next)); };
  // The print document is built only while the print options are open or the browser is printing (Ctrl+P included).
  const [printPanelOpen, setPrintPanelOpen] = useState(false);
  const [browserPrinting, setBrowserPrinting] = useState(false);
  useEffect(() => {
    const before = () => flushSync(() => setBrowserPrinting(true));
    const after = () => setBrowserPrinting(false);
    window.addEventListener("beforeprint", before);
    window.addEventListener("afterprint", after);
    return () => { window.removeEventListener("beforeprint", before); window.removeEventListener("afterprint", after); };
  }, []);
  return (
    <div className="timetable-views">
      {timetable.sessions && (
        <div className="timetable-controls timetable-term">
          <ChipGroup label={text.termLabel} caption={text.termLabel} value={term} onChange={(value) => setTerm(value === 2 ? 2 : 1)}
            options={([1, 2] as const).map((value) => ({ value, label: text.terms[value] }))} />
          <span className="card-note">{text.termHint}</span>
        </div>
      )}
      {!editing && (
        <div className="timetable-controls">
          <a className="link-button" href={`/api/v1/timetables/${timetable.summary.id}/export.xlsx${timetable.sessions ? `?term=${term}` : ""}`} download>
            <FileDown aria-hidden="true" size={18} /><span>{text.exportExcel}</span>
          </a>
        </div>
      )}
      {!editing && <PrintPanel options={print} view={view} onChange={setPrint} onToggle={setPrintPanelOpen} />}
      <div className="timetable-controls">
        <Field id="timetable-view" label={text.viewsLabel}>
          <Select id="timetable-view" value={view} onChange={(event) => chooseView(event.target.value as View)}
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
      {view === "section" && section && editing && (
        <TimetableEditor timetable={timetable} sectionId={section.id} format={format} look={look} lessonCount={lessonCountOf(section.shiftId)}
          sessions={sessionViewOf(timetable, section.shiftId, term)}
          onClose={() => setEditing(false)} onSaved={(id) => { setEditing(false); onSaved(id); }} />
      )}
      {view === "section" && section && !editing && (
        <WeekGrid caption={look.sectionName(section.id)} days={timetable.days} shift={shifts.get(section.shiftId)} format={format} look={look}
          lessonCount={lessonCountOf(section.shiftId)} lessons={timetable.lessons.filter((lesson) => lesson.sectionId === section.id)}
          sessions={sessionViewOf(timetable, section.shiftId, term)}
          secondLine={(lesson) => look.teacher(lesson.teacherId)?.shortName ?? ""}
          cell={(lesson, day, number) => flagCell(cellFor(lesson, day, number, format, look, (item) => look.teacher(item.teacherId)?.shortName ?? ""), flagged.has(slotKey(section.id, day, number)))} />
      )}
      {view === "teacher" && teacher && (
        <>
          <p className="card-note">{text.teacherLoad(format.count(teacherLessons.length, "lesson"))}</p>
          {teacherShifts.map((shiftId) => (
            <WeekGrid key={shiftId} caption={teacherShifts.length > 1 ? text.teacherInShift(teacher.name, shifts.get(shiftId)?.name ?? "") : teacher.name}
              days={timetable.days} shift={shifts.get(shiftId)} format={format} look={look} lessonCount={lessonCountOf(shiftId)}
              sessions={sessionViewOf(timetable, shiftId, term)}
              lessons={teacherLessons.filter((lesson) => look.section(lesson.sectionId)?.shiftId === shiftId)}
              secondLine={(lesson) => look.sectionName(lesson.sectionId)}
              cell={(lesson, day, number) => flagCell(cellFor(lesson, day, number, format, look, (item) => look.sectionName(item.sectionId)),
                lesson !== undefined && flagged.has(slotKey(lesson.sectionId, day, number)))} />
          ))}
        </>
      )}
      {view === "master" && <MasterGrid timetable={timetable} format={format} look={look} term={term} flagged={editing ? undefined : flagged} />}
      {!editing && (printPanelOpen || browserPrinting) && <PrintDocument timetable={timetable} options={print} term={term} format={format} look={look}
        current={{ view, sectionId: section?.id, teacherId: teacher?.id }} />}
    </div>
  );
}

/** MF11: marks a cell that conflicts with today's data (icon and border, with the reason in its description). */
function flagCell<T extends { state?: string; description: string }>(cell: T, flagged: boolean): T {
  return flagged ? { ...cell, state: "conflict", description: currentText.cellConflict(cell.description) } : cell;
}

const printText = messages.school.printing;

/** MF5: what to print, on which paper and orientation, and «ملاءمة الصفحة»; choosing what to print restores its defaults. */
type PrintPanelProps = { options: PrintOptions; view: View; onChange: (options: PrintOptions) => void; onToggle: (open: boolean) => void };

function PrintPanel({ options, view, onChange, onToggle }: PrintPanelProps) {
  return (
    <details className="advanced-options tool-panel print-options" onToggle={(event) => onToggle(event.currentTarget.open)}>
      <summary><Printer aria-hidden="true" size={18} /><span>{printText.title}</span></summary>
      <div className="print-options-fields">
        <Field id="print-scope" label={printText.scope}>
          <Select id="print-scope" value={options.scope} onChange={(event) => onChange(defaultOptions(event.target.value as PrintScope, view))}
            options={(["current", "sections", "teachers", "school"] as const).map((value) => ({ value, label: printText.scopes[value] }))} />
        </Field>
        <Field id="print-paper" label={printText.paper}>
          <Select id="print-paper" value={options.paper} onChange={(event) => onChange({ ...options, paper: event.target.value as PaperSize })}
            options={(["A4", "A3"] as const).map((value) => ({ value, label: value }))} />
        </Field>
        <Field id="print-orientation" label={printText.orientation}>
          <Select id="print-orientation" value={options.orientation} onChange={(event) => onChange({ ...options, orientation: event.target.value as Orientation })}
            options={(["portrait", "landscape"] as const).map((value) => ({ value, label: printText.orientations[value] }))} />
        </Field>
      </div>
      <Checkbox checked={options.fit} onChange={(event) => onChange({ ...options, fit: event.target.checked })}>{printText.fit}</Checkbox>
      <div>
        <Button icon={<Printer aria-hidden="true" size={18} />} onClick={() => window.print()}>{printText.print}</Button>
      </div>
      <p className="card-note">{printText.hint}</p>
    </details>
  );
}

/** The lifecycle status as a badge: the icon says the same as the colour. */
function StatusBadge({ status }: { status: TimetableVersionSummary["status"] }) {
  if (status === "approved") return <Badge tone="success" icon={<BadgeCheck aria-hidden="true" size={16} />}>{lifecycle.statuses.approved}</Badge>;
  if (status === "archived") return <Badge icon={<Archive aria-hidden="true" size={16} />}>{lifecycle.statuses.archived}</Badge>;
  return <Badge icon={<FilePen aria-hidden="true" size={16} />}>{lifecycle.statuses.draft}</Badge>;
}

function VersionsTable({ versions, selectedId, onSelect }: { versions: TimetableVersionSummary[]; selectedId: number | undefined; onSelect: (id: number) => void }) {
  const format = useFormatter();
  const columns: TableColumn<TimetableVersionSummary>[] = [
    { key: "number", header: text.versionColumn, cell: (row) => text.version(format.number(row.number)) },
    { key: "source", header: text.sourceColumn, cell: (row) => text.sources[row.source] },
    { key: "date", header: text.createdAt, cell: (row) => format.date(row.createdAt.slice(0, 10)) },
    { key: "score", header: text.score, numeric: true, cell: (row) => (row.score === null ? messages.school.generation.none : format.number(row.score)) },
    { key: "status", header: text.statusColumn, cell: (row) => <StatusBadge status={row.status} /> },
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
  const archive = useArchiveTimetable();
  const rollback = useRollbackTimetable();
  const feedback = useFormFeedback();
  const [confirming, setConfirming] = useState<"approve" | "archive" | "rollback" | null>(null);
  const [editing, setEditing] = useState(false);
  const summary = timetable.data?.summary;
  // MF11: the saved version against today's data; read fresh on every visit.
  const currentCheck = useCurrentCheck(selectedId);
  const replaceTeachers = useReplaceTeachers();
  const startRepair = useStartRepair(yearId);
  const navigate = useNavigate();
  const [focusRequest, setFocusRequest] = useState<{ nonce: number; item: FindingItem } | null>(null);
  const flagged = useMemo(() => flaggedSlots(currentCheck.data?.findings ?? [], timetable.data?.lessons ?? []), [currentCheck.data, timetable.data]);
  const hasConflicts = (currentCheck.data?.findings.length ?? 0) > 0;
  // Comparison: the version in the URL (?compare=) is the older side; it defaults to the parent, else the previous version.
  const compareParam = Number(params.get("compare")) || undefined;
  const defaultBase = (current: TimetableVersionSummary) =>
    list.find((item) => item.id === current.parentVersionId)?.id ?? list.find((item) => item.number < current.number)?.id ?? list.find((item) => item.id !== current.id)?.id;
  const compareBase = summary && compareParam !== undefined && list.some((item) => item.id === compareParam && item.id !== summary.id) ? compareParam : undefined;
  const selectVersion = (id: number) => { setEditing(false); setParams({ version: String(id) }); };
  const versionLabel = (row: TimetableVersionSummary) =>
    lifecycle.versionOption(format.number(row.number), format.date(row.createdAt.slice(0, 10)), lifecycle.statuses[row.status], text.sources[row.source]);
  const startCompare = () => {
    const base = summary ? defaultBase(summary) : undefined;
    if (summary && base !== undefined) setParams({ version: String(summary.id), compare: String(base) });
  };

  return (
    <div className="page">
      <PageHeader icon={CalendarCheck} title={text.title} description={text.description} />
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
      {list.length > 0 && summary && timetable.data && compareBase !== undefined && (
        <VersionComparison target={timetable.data} versions={list} baseId={compareBase}
          onBaseChange={(id) => setParams({ version: String(summary.id), compare: String(id) })}
          onClose={() => setParams({ version: String(summary.id) })} />
      )}
      {list.length > 0 && (
        <Card className="page-card timetable-versions" aria-labelledby="timetable-versions-title">
          <SectionTitle level={2} icon={History} id="timetable-versions-title">{text.versions}</SectionTitle>
          <div className="version-bar">
            <Field id="timetable-version" label={text.versionColumn}>
              <Select id="timetable-version" value={selectedId === undefined ? "" : String(selectedId)} disabled={editing}
                onChange={(event) => selectVersion(Number(event.target.value))}
                options={list.map((row) => ({ value: String(row.id), label: versionLabel(row) }))} />
            </Field>
            {summary && <StatusBadge status={summary.status} />}
            {summary && !editing && (
              <div className="version-actions">
                {summary.status !== "archived" && (
                  <Button icon={<PencilLine aria-hidden="true" size={18} />} onClick={() => setEditing(true)}>{text.edit}</Button>
                )}
                {summary.status === "draft" && <Button icon={<Stamp aria-hidden="true" size={18} />} disabled={hasConflicts} aria-describedby={hasConflicts ? "approval-blocked" : undefined}
                  onClick={() => setConfirming("approve")}>{text.approve}</Button>}
                {summary.status !== "archived" && (
                  <Button variant="secondary" icon={<Archive aria-hidden="true" size={18} />} onClick={() => setConfirming("archive")}>{lifecycle.archive}</Button>
                )}
                <Button variant="secondary" icon={<ArchiveRestore aria-hidden="true" size={18} />} onClick={() => setConfirming("rollback")}>{lifecycle.rollback}</Button>
                {defaultBase(summary) !== undefined && (
                  <Button variant="secondary" icon={<GitCompareArrows aria-hidden="true" size={18} />} onClick={startCompare}>{lifecycle.compare}</Button>
                )}
              </div>
            )}
          </div>
          {summary && <p className="card-note">{editing ? lifecycle.editingHint : lifecycle.statusHints[summary.status]}</p>}
          {summary && summary.status === "draft" && hasConflicts && !editing && <Alert tone="warning" message={currentText.approvalBlocked} />}
          <details className="advanced-options tool-panel">
            <summary><History aria-hidden="true" size={18} /><span>{lifecycle.allVersions(format.count(list.length, "version"))}</span></summary>
            <VersionsTable versions={list} selectedId={selectedId} onSelect={selectVersion} />
          </details>
        </Card>
      )}
      {list.length > 0 && (
          <Card className="page-card timetable-main" aria-labelledby="timetable-main-title">
            {timetable.isError && <Alert tone="error" message={text.loadFailed}>{userErrorMessage(timetable.error)}</Alert>}
            {summary && timetable.data && (
              <>
                <div className="generation-heading">
                  <SectionTitle level={2} icon={CalendarRange} id="timetable-main-title">{text.version(format.number(summary.number))}</SectionTitle>
                </div>
                {timetable.data.violations === 0
                  ? <p className="generation-verified"><ShieldCheck aria-hidden="true" size={18} /><span>{text.verified}</span></p>
                  : <Alert tone="error" message={messages.errors.TIMETABLE_VERIFICATION_FAILED} />}
                {!editing && (
                  <CurrentDataCheck version={summary} check={currentCheck}
                    onShow={(item) => setFocusRequest((current) => ({ nonce: (current?.nonce ?? 0) + 1, item }))}
                    repairing={startRepair.isPending} replacing={replaceTeachers.isPending}
                    onRepair={() => { feedback.reset(); startRepair.mutate(summary.id, { onSuccess: () => navigate("/timetable/generate"), onError: feedback.showError }); }}
                    onReplace={() => {
                      feedback.reset();
                      replaceTeachers.mutate(summary, {
                        onSuccess: (created) => { setParams({ version: String(created.id) }); feedback.showSuccess(currentText.replaced); },
                        onError: (error) => { feedback.showError(error); void currentCheck.refetch(); },
                      });
                    }} />
                )}
                <TimetableViews key={summary.id} timetable={timetable.data} editing={editing} onEditingChange={setEditing} flagged={flagged} focusRequest={focusRequest}
                  onSaved={(id) => { setEditing(false); setParams({ version: String(id) }); feedback.showSuccess(text.savedEdit); }} />
              </>
            )}
            {!summary && timetable.isPending && <p>{messages.app.loading}</p>}
          </Card>
      )}
      {summary && (
        <>
          <ConfirmDialog open={confirming === "approve"} title={text.approveConfirmTitle} consequence={`${text.approveConfirm(format.number(summary.number))}${currentCheck.data?.stale && !hasConflicts ? ` ${currentText.approvalWarning}` : ""}`}
            confirmLabel={text.approve} confirmIcon={<Stamp aria-hidden="true" size={18} />} loading={approve.isPending}
            onCancel={() => setConfirming(null)}
            onConfirm={() => approve.mutate(summary, {
              onSuccess: () => { setConfirming(null); feedback.showSuccess(list.some((item) => item.isApproved) ? lifecycle.approveDoneArchived : text.approvedDone); },
              onError: (error) => { setConfirming(null); feedback.showError(error); },
            })} />
          <ConfirmDialog open={confirming === "archive"} title={lifecycle.archiveConfirmTitle} danger
            consequence={summary.status === "approved" ? lifecycle.archiveConfirmApproved(format.number(summary.number)) : lifecycle.archiveConfirm(format.number(summary.number))}
            confirmLabel={lifecycle.archive} confirmIcon={<Archive aria-hidden="true" size={18} />} loading={archive.isPending}
            onCancel={() => setConfirming(null)}
            onConfirm={() => archive.mutate(summary, {
              onSuccess: () => { setConfirming(null); feedback.showSuccess(lifecycle.archivedDone); },
              onError: (error) => { setConfirming(null); feedback.showError(error); },
            })} />
          <ConfirmDialog open={confirming === "rollback"} title={lifecycle.rollbackConfirmTitle} consequence={lifecycle.rollbackConfirm(format.number(summary.number))}
            confirmLabel={lifecycle.rollback} confirmIcon={<ArchiveRestore aria-hidden="true" size={18} />} loading={rollback.isPending}
            onCancel={() => setConfirming(null)}
            onConfirm={() => rollback.mutate(summary, {
              onSuccess: (created) => { setConfirming(null); setParams({ version: String(created.id) }); feedback.showSuccess(lifecycle.rollbackDone(format.number(created.number))); },
              onError: (error) => { setConfirming(null); feedback.showError(error); },
            })} />
        </>
      )}
      {versions.isPending && yearId && <p>{messages.app.loading}</p>}
    </div>
  );
}
