import { DatabaseBackup } from "lucide-react";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { BackupSection } from "./BackupSection";

/** «النسخ الاحتياطي»: the backup folder and automatic copies, the list of copies and the restore. */
export function BackupSettingsPage() {
  return (
    <div className="page page-full">
      <PageHeader icon={DatabaseBackup} title={messages.school.nav.settingsBackup} description={messages.app.backupDescription} />
      <div className="settings-grid">
        <BackupSection />
      </div>
    </div>
  );
}
