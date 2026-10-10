import { SectionTitle } from "../../components/ui/section-title";
import { Palette } from "lucide-react";
import { TimetableCell } from "../../components/ui/timetable-cell";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";
import { sampleSubjects } from "./sampleData";

const [math, arabic, , english, physics] = sampleSubjects;

const states = [
  { state: "normal", label: guideMessages.stateNormal, subject: math },
  { state: "selected", label: guideMessages.stateSelected, subject: arabic },
  { state: "conflict", label: guideMessages.stateConflict, subject: physics, description: guideMessages.conflictDescription },
  { state: "blocked", label: guideMessages.stateBlocked, subject: undefined, description: guideMessages.blockedDescription },
  { state: "drag-valid", label: guideMessages.stateDragValid, subject: english },
  { state: "drag-invalid", label: guideMessages.stateDragInvalid, subject: english },
] as const;

export function TimetableSection() {
  return (
    <GuideSection id="guide-timetable" title={guideMessages.timetable}>
      <div className="guide-cells">
        {states.map((item) => (
          <figure key={item.state} className="guide-cell-figure">
            <TimetableCell
              state={item.state}
              subject={item.subject?.name}
              teacher={item.subject?.teacher}
              color={item.subject?.color}
              description={"description" in item ? item.description : `${item.label}: ${item.subject?.name ?? ""}`}
            />
            <figcaption className="type-caption">{item.label}</figcaption>
          </figure>
        ))}
      </div>
      <SectionTitle level={3} icon={Palette}>{guideMessages.subjectsHeading}</SectionTitle>
      <div className="guide-cells">
        {sampleSubjects.map((subject) => (
          <TimetableCell
            key={subject.name}
            subject={subject.name}
            teacher={subject.teacher}
            color={subject.color}
            description={`${subject.name}، ${subject.teacher}`}
          />
        ))}
      </div>
    </GuideSection>
  );
}
