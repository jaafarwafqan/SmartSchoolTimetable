import { useState } from "react";
import { DateField } from "../../components/DateField";
import { InlineAddForm } from "../../components/InlineAddForm";
import { TextField } from "../../components/TextField";
import { TimeField } from "../../components/TimeField";
import { ExpandableRow } from "../../components/ui/expandable-row";
import { LtrText } from "../../components/ui/ltr-text";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const text = guideMessages.patterns;

/** DESIGN_SYSTEM.md 14 and 15: inline add row, details in place, date/time fields, LTR runs. */
export function PatternsSection() {
  const [items, setItems] = useState<string[]>([text.sampleItem]);
  const [open, setOpen] = useState<string | null>(null);
  return (
    <GuideSection id="guide-patterns" title={text.title}>
      <p>{text.inlineRule}</p>
      <InlineAddForm label={text.inlineLabel} buttonLabel={text.add} pending={false}
        onSubmit={(form, element) => {
          const name = String(form.get("guide-new-item") ?? "").trim();
          if (name) setItems([...items, name]);
          element.reset();
        }}>
        <TextField id="guide-new-item" label={text.inlineField} />
      </InlineAddForm>
      <p>{text.expandRule}</p>
      <ul className="expandable-list" aria-label={text.listLabel}>
        {items.map((item, index) => (
          <ExpandableRow key={`${item}-${index}`} id={`guide-item-${index}`} summary={<strong>{item}</strong>}
            expanded={open === `${item}-${index}`} onToggle={() => setOpen(open === `${item}-${index}` ? null : `${item}-${index}`)}>
            <p>{text.details}</p>
          </ExpandableRow>
        ))}
      </ul>
      <p>{text.dialogRule}</p>
      <p>{text.bulkRule}</p>
      <div className="guide-grid">
        <DateField id="guide-date" label={text.date} defaultValue="2026-09-21" />
        <TimeField id="guide-time" label={text.time} defaultValue="08:00" />
        <p>{text.yearLabel} <LtrText>{text.yearSample}</LtrText></p>
      </div>
    </GuideSection>
  );
}
