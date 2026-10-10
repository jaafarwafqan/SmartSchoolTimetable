import { SectionTitle } from "../../components/ui/section-title";
import { Images, School } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../components/ui/alert";
import { Card } from "../../components/ui/card";
import { Spinner } from "../../components/ui/spinner";
import { userErrorMessage } from "../../api";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { AssetPanel } from "./AssetPanel";
import { ProfileForm } from "./ProfileForm";
import { useSchoolProfile } from "./profileApi";

/** بيانات المدرسة: profile, display preferences, logo and stamp. */
export function SchoolProfilePage() {
  const profile = useSchoolProfile();
  // The form is remounted only after an explicit reload (conflict recovery), so saved values and
  // success messages are kept after a normal save.
  const [formKey, setFormKey] = useState(0);
  const reload = () => {
    void profile.refetch().then(() => setFormKey((key) => key + 1));
  };
  return (
    <div className="page">
      <PageHeader icon={School} title={messages.school.nav.profile} description={messages.school.profile.description} />
      {profile.isPending && <Spinner label={messages.school.common.loading} />}
      {profile.isError && <Alert tone="error" message={userErrorMessage(profile.error)} />}
      {profile.data && (
        <div className="profile-layout">
          <Card className="page-card">
            <ProfileForm key={formKey} profile={profile.data} onReload={reload} reloading={profile.isFetching} />
          </Card>
          <Card className="page-card" aria-labelledby="images-title">
            <SectionTitle level={2} icon={Images} id="images-title">{messages.school.profile.imagesTitle}</SectionTitle>
            <div className="asset-grid">
              <AssetPanel kind="logo" profile={profile.data} onReload={reload} reloading={profile.isFetching} />
              <AssetPanel kind="stamp" profile={profile.data} onReload={reload} reloading={profile.isFetching} />
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
