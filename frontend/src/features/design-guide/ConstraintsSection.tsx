import { useState } from "react";
import { BlockedPeriodsGrid, type GridSlot } from "../../components/ui/blocked-periods-grid";
import { Field } from "../../components/ui/field";
import { SubjectColorPicker } from "../../components/ui/subject-color-picker";
import { Textarea } from "../../components/ui/textarea";
import { formatNumber } from "../../lib/format";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const days = guideMessages.gridDays.map((name, index) => ({ day: [7, 1, 2][index], name }));

/** Textarea, subject colour picker and the keyboard blocked-periods grid (2D/2E primitives). */
export function ConstraintsSection() {
  const [color, setColor] = useState(4);
  const [blocked, setBlocked] = useState<GridSlot[]>([{ day: 1, lessonNumber: 2 }]);
  return (
    <GuideSection id="guide-constraints" title={guideMessages.constraints}>
      <div className="guide-grid">
        <Field id="guide-notes" label={guideMessages.sampleNotes}>
          <Textarea id="guide-notes" />
        </Field>
        <SubjectColorPicker name="guide-color" legend={guideMessages.colorLegend} value={color}
          swatchLabel={(index) => guideMessages.colorSwatch(formatNumber(index))} onChange={setColor} />
      </div>
      <BlockedPeriodsGrid
        label={guideMessages.gridLabel}
        days={days}
        lessons={5}
        lessonLabel={(lesson) => guideMessages.gridLesson(formatNumber(lesson))}
        cellLabel={(day, lesson, isBlocked) => guideMessages.gridCell(day, formatNumber(lesson), isBlocked)}
        value={blocked}
        onChange={setBlocked}
      />
    </GuideSection>
  );
}
