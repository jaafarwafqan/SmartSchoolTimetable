import { useMemo, useState } from "react";
import { Archive, Layers3, Plus, RotateCcw } from "lucide-react";
import { apiRequest, userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Input } from "../../components/ui/input";
import { Select } from "../../components/ui/select";
import { PageHeader } from "../../layout/PageHeader";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useAcademicYears } from "../academic-years/yearsApi";
import { useShifts, type Shift } from "../timetable-structure/scheduleApi";
import { useSectionSave, useSections, useStageSave, useStages, type Stage, type Section } from "./stagesApi";

const text = messages.school.stagesSections;
export function StagesSectionsPage() {
  const format = useFormatter();
  const years = useAcademicYears({ search: "", page: 1, pageSize: 100 });
  const [yearId, setYearId] = useState<number>();
  const currentYear = years.data?.items.find(row => row.isCurrent)?.id ?? years.data?.items[0]?.id;
  const selectedYear = yearId ?? currentYear;
  const [selectedStage, setSelectedStage] = useState<number>();
  const [includeArchived, setIncludeArchived] = useState(false);
  const stages = useStages(selectedYear, includeArchived);
  const stageId = selectedStage ?? stages.data?.items.find(row => !row.isArchived)?.id;
  const sections = useSections(selectedYear, stageId, includeArchived);
  const shifts = useShifts(selectedYear);
  const [stageName, setStageName] = useState("");
  const [stageOrder, setStageOrder] = useState("1");
  const [sectionLabel, setSectionLabel] = useState("");
  const [shiftId, setShiftId] = useState<number>();
  const [students, setStudents] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);
  const createStage = useStageSave(selectedYear ?? 0);
  const createSection = useSectionSave(selectedYear ?? 0, stageId ?? 0);
  const yearsError = years.isError || stages.isError || sections.isError || shifts.isError;

  async function toggleStage(stage: Stage) {
    setActionError(null);
    const endpoint = `/api/v1/academic-years/${selectedYear}/stages/${stage.id}/${stage.isArchived ? "restore" : "archive"}`;
    try {
      await apiRequest(endpoint, "POST", { version: stage.version });
      await stages.refetch();
      await sections.refetch();
    } catch (error) { setActionError(userErrorMessage(error)); }
  }
  async function toggleSection(section: Section) {
    setActionError(null);
    const endpoint = `/api/v1/academic-years/${selectedYear}/stages/${stageId}/sections/${section.id}/${section.isArchived ? "restore" : "archive"}`;
    try { await apiRequest(endpoint, "POST", { version: section.version }); await sections.refetch(); } catch (error) { setActionError(userErrorMessage(error)); }
  }
  const shiftOptions = useMemo(() => (shifts.data?.items ?? []).map((shift: Shift) => ({ value: String(shift.id), label: shift.name })), [shifts.data]);
  const selected = stages.data?.items.find(row => row.id === stageId);
  return (
    <section className="page-stack">
      <PageHeader title={text.title} description={text.description} />
      {yearsError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {actionError && <Alert tone="error" message={actionError} />}
      <Card>
        <div className="page-header-row"><h2>{text.stages}</h2><Select aria-label={text.year} value={selectedYear ?? ""} options={(years.data?.items ?? []).map(year => ({ value: String(year.id), label: year.label }))} onChange={event => { setYearId(Number(event.target.value)); setSelectedStage(undefined); }} /></div>
        <div className="schedule-create-row"><Field id="stage-name" label={text.stageName}><Input id="stage-name" value={stageName} onChange={event => setStageName(event.target.value)} /></Field><Field id="stage-order" label={text.order}><Input id="stage-order" type="number" min="1" max="999" value={stageOrder} onChange={event => setStageOrder(event.target.value)} /></Field><Button icon={<Plus size={18} />} disabled={!selectedYear || !stageName.trim()} loading={createStage.isPending} onClick={() => createStage.mutate({ body: { name: stageName, displayOrder: Number(stageOrder), version: 0 } }, { onSuccess: () => { setStageName(""); void stages.refetch(); } })}>{text.addStage}</Button></div>
        <Checkbox checked={includeArchived} onChange={event => setIncludeArchived(event.target.checked)}>{text.includeArchived}</Checkbox>
        <div className="schedule-shift-list">{(stages.data?.items ?? []).map(stage => <div className="schedule-stage-row" key={stage.id}><Button variant={stageId === stage.id ? "primary" : "secondary"} icon={<Layers3 size={18} />} aria-pressed={stageId === stage.id} onClick={() => setSelectedStage(stage.id)}>{stage.name}{stage.isArchived && <small>{text.archived}</small>}</Button><Button variant="ghost" icon={stage.isArchived ? <RotateCcw size={18} /> : <Archive size={18} />} aria-label={`${stage.isArchived ? text.restore : text.archive}: ${stage.name}`} onClick={() => void toggleStage(stage)}>{stage.isArchived ? text.restore : text.archive}</Button></div>)}</div>
        {(stages.data?.items.length ?? 0) === 0 && <p>{text.noStages}</p>}
      </Card>
      {selected && <Card>
        <div className="page-header-row"><h2>{text.sections} <span>{selected.name}</span></h2></div>
        <div className="schedule-create-row"><Field id="section-label" label={text.label}><Input id="section-label" value={sectionLabel} onChange={event => setSectionLabel(event.target.value)} /></Field><Field id="section-shift" label={text.shift}><Select id="section-shift" value={shiftId ?? ""} options={shiftOptions} onChange={event => setShiftId(Number(event.target.value))} /></Field><Field id="section-students" label={text.students}><Input id="section-students" type="number" min="0" max="200" value={students} onChange={event => setStudents(event.target.value)} /></Field><Button icon={<Plus size={18} />} disabled={!sectionLabel.trim() || !shiftId} loading={createSection.isPending} onClick={() => createSection.mutate({ body: { label: sectionLabel, shiftId: shiftId ?? 0, studentCount: students ? Number(students) : null, version: 0 } }, { onSuccess: () => { setSectionLabel(""); setStudents(""); void sections.refetch(); } })}>{text.addSection}</Button></div>
        {(sections.data?.items ?? []).length === 0 && <p>{text.noSections}</p>}
        <div className="section-capacity-list">{(sections.data?.items ?? []).map(section => <div className="section-capacity-row" key={section.id}><strong>{section.label}{section.isArchived && <small>{text.archived}</small>}</strong><span>{text.capacity} <bdi>{format.number(section.weeklyCapacity)}</bdi></span><span>{text.students} <bdi>{section.studentCount === null ? "" : format.number(section.studentCount)}</bdi></span><Button variant="ghost" icon={section.isArchived ? <RotateCcw size={18} /> : <Archive size={18} />} aria-label={`${section.isArchived ? text.restore : text.archive} ${section.label}`} onClick={() => void toggleSection(section)}>{section.isArchived ? text.restore : text.archive}</Button></div>)}</div>
      </Card>}
    </section>
  );
}
