import { ShieldAlert } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert } from "../../components/ui/alert";
import { messages } from "../../i18n/messages";
import { useFormatter, useSchoolContext } from "../../lib/schoolContext";
import { useCurrentCheck, useTimetableVersions } from "../timetable/timetableApi";

const text = messages.school.dashboard;

/**
 * The approved timetable against today's school data (MF11): when something changed after approval and now conflicts (a teacher released, a day
 * blocked, a lesson count changed) the dashboard says so, with a way to open it. Nothing is shown while the approved version still fits.
 */
export function ApprovedVersionAlert() {
  const format = useFormatter();
  const year = useSchoolContext().data?.currentYear;
  const versions = useTimetableVersions(year?.id);
  const approved = versions.data?.find((version) => version.status === "approved");
  const check = useCurrentCheck(approved?.id);
  const conflicts = check.data?.findings.length ?? 0;
  if (!approved || conflicts === 0) return null;
  return (
    <div className="dashboard-wide">
      <Alert tone="warning" message={text.approvedConflicts(format.count(conflicts, "conflict"))}>
        <span>
          <Link className="link-button" to="/timetable/view"><ShieldAlert aria-hidden="true" size={18} /><span>{text.openApproved}</span></Link>
        </span>
      </Alert>
    </div>
  );
}
