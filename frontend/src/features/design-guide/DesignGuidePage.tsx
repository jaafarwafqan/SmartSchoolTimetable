import { ButtonsSection } from "./ButtonsSection";
import { ColorTokensSection } from "./ColorTokensSection";
import { ConstraintsSection } from "./ConstraintsSection";
import { DialogSection } from "./DialogSection";
import { FeedbackSection } from "./FeedbackSection";
import { FieldsSection } from "./FieldsSection";
import { guideMessages } from "./guideMessages";
import { NavigationSection } from "./NavigationSection";
import { PatternsSection } from "./PatternsSection";
import { SchedulingSection } from "./SchedulingSection";
import { TableSection } from "./TableSection";
import { TimetableSection } from "./TimetableSection";
import { TypographySection } from "./TypographySection";

/**
 * Development-only style guide (DESIGN_SYSTEM.md 12.4). Registered only when import.meta.env.DEV is true,
 * so it is excluded from the production bundle and from navigation.
 */
export default function DesignGuidePage() {
  return (
    <main className="app-main">
      <div className="page">
        <header className="page-header">
          <h1>{guideMessages.title}</h1>
          <p>{guideMessages.description}</p>
        </header>
        <ColorTokensSection />
        <TypographySection />
        <ButtonsSection />
        <FieldsSection />
        <ConstraintsSection />
        <PatternsSection />
        <SchedulingSection />
        <FeedbackSection />
        <TableSection />
        <DialogSection />
        <NavigationSection />
        <TimetableSection />
      </div>
    </main>
  );
}
