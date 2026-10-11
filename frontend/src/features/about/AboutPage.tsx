import { useMutation } from "@tanstack/react-query";
import { FileDown, FolderOpen, Info, LifeBuoy, Scale, Boxes } from "lucide-react";
import { apiRequest } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { LtrText } from "../../components/ui/ltr-text";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { arabicLicense, useAbout, type ThirdPartyLicense } from "./aboutApi";

const text = messages.school.about;

const thirdPartyColumns: readonly TableColumn<ThirdPartyLicense>[] = [
  { key: "name", header: text.thirdParty.component, cell: (item) => <LtrText>{item.name}</LtrText> },
  { key: "license", header: text.thirdParty.license, cell: (item) => <LtrText>{item.license}</LtrText> },
];

/** «حول البرنامج»: what is installed, the licence, where the data is, the support log and the third-party licences. */
export function AboutPage() {
  const format = useFormatter();
  const about = useAbout();
  const feedback = useFormFeedback();
  const openFolder = useMutation({ mutationFn: () => apiRequest<void>("/api/v1/about/open-data-folder", "POST", {}) });
  const data = about.data;
  return (
    <div className="page">
      <PageHeader icon={Info} title={text.title} description={text.description} />
      {about.isError && <Alert tone="error" message={text.loadFailed} />}
      {about.isPending && <Spinner label={messages.app.loadingContent} />}
      {data && (
        <div className="settings-grid">
          <Card className="settings-section" aria-labelledby="about-facts-title">
            <SectionTitle level={2} icon={Info} id="about-facts-title">{text.appName}</SectionTitle>
            <p className="card-note">{text.tagline}</p>
            <dl className="about-facts">
              <div><dt>{text.facts.version}</dt><dd><LtrText>{data.version}</LtrText></dd></div>
              <div><dt>{text.facts.buildDate}</dt><dd>{format.date(data.buildDate)}</dd></div>
              <div><dt>{text.facts.schema}</dt><dd>{format.number(data.schemaVersion)}</dd></div>
              <div><dt>{text.facts.developer}</dt><dd>{text.facts.developerName}</dd></div>
            </dl>
          </Card>

          <Card className="settings-section" aria-labelledby="about-data-title">
            <SectionTitle level={2} icon={FolderOpen} id="about-data-title">{text.dataFolder.title}</SectionTitle>
            <p className="card-note">{text.dataFolder.hint}</p>
            <Alert tone="error" message={feedback.error} />
            <output className="folder-path is-single-line" dir="ltr" title={data.dataFolder}>{data.dataFolder}</output>
            <div className="form-actions">
              <Button variant="secondary" icon={<FolderOpen aria-hidden="true" size={18} />} loading={openFolder.isPending}
                onClick={() => { feedback.reset(); openFolder.mutate(undefined, { onError: feedback.showError }); }}>
                {text.dataFolder.open}
              </Button>
            </div>
          </Card>

          <Card className="settings-section" aria-labelledby="about-license-title">
            <SectionTitle level={2} icon={Scale} id="about-license-title">{text.license.title}</SectionTitle>
            <p className="about-license">{arabicLicense(data.license)}</p>
            <p className="card-note">{text.license.fullTextNote}</p>
          </Card>

          <Card className="settings-section" aria-labelledby="about-support-title">
            <SectionTitle level={2} icon={LifeBuoy} id="about-support-title">{text.support.title}</SectionTitle>
            <p className="card-note">{text.support.hint}</p>
            <div className="form-actions">
              <a className="link-button" href="/api/v1/about/support-log" download>
                <FileDown aria-hidden="true" size={18} /><span>{text.support.export}</span>
              </a>
            </div>
          </Card>

          <Card className="settings-section" aria-labelledby="about-third-title">
            <SectionTitle level={2} icon={Boxes} id="about-third-title">{text.thirdParty.title}</SectionTitle>
            <p className="card-note">{text.thirdParty.hint}</p>
            <DataTable caption={text.thirdParty.title} columns={thirdPartyColumns} rows={data.thirdParty} rowKey={(item) => item.name} scrollable />
          </Card>
        </div>
      )}
    </div>
  );
}
