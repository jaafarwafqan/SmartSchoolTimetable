import { CircleAlert, CircleCheck, Info, TriangleAlert } from "lucide-react";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

export function FeedbackSection() {
  return (
    <GuideSection id="guide-alerts" title={`${guideMessages.alerts} · ${guideMessages.badges}`}>
      <div className="guide-stack">
        <Alert tone="info" message={guideMessages.alertInfo} />
        <Alert tone="success" message={guideMessages.alertSuccess} />
        <Alert tone="warning" message={guideMessages.alertWarning} />
        <Alert tone="error" message={guideMessages.alertError} />
      </div>
      <div className="guide-row">
        <Badge tone="primary" icon={<Info aria-hidden="true" size={16} />}>{guideMessages.variantPrimary}</Badge>
        <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{guideMessages.alertSuccess}</Badge>
        <Badge tone="warning" icon={<TriangleAlert aria-hidden="true" size={16} />}>{guideMessages.stateConflict}</Badge>
        <Badge tone="danger" icon={<CircleAlert aria-hidden="true" size={16} />}>{guideMessages.stateError}</Badge>
        <Badge>{guideMessages.stateNormal}</Badge>
        <Spinner label={messages.app.loadingContent} />
      </div>
    </GuideSection>
  );
}
