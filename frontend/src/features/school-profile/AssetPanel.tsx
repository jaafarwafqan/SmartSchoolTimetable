import { SectionTitle } from "../../components/ui/section-title";
import { ImageOff, Trash2, Image } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { FileUpload } from "../../components/ui/file-upload";
import { messages } from "../../i18n/messages";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useRemoveAsset, useUploadAsset, type AssetKind, type SchoolProfile } from "./profileApi";

const text = messages.school.profile;
const maxMegabytes = 2;

type AssetPanelProps = {
  kind: AssetKind;
  profile: SchoolProfile;
  onReload: () => void;
  reloading: boolean;
};

/** Logo or stamp: preview, upload (PNG/JPEG/WebP up to 2 MB, validated by the server) and removal. */
export function AssetPanel({ kind, profile, onReload, reloading }: AssetPanelProps) {
  const feedback = useFormFeedback();
  const format = useFormatter();
  const upload = useUploadAsset(kind);
  const remove = useRemoveAsset(kind);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const label = kind === "logo" ? text.logo : text.stamp;
  const hasImage = kind === "logo" ? profile.hasLogo : profile.hasStamp;
  const hintId = `${kind}-hint`;

  return (
    <section className="asset-panel" aria-labelledby={`${kind}-title`}>
      <SectionTitle level={3} icon={Image} id={`${kind}-title`}>{label}</SectionTitle>
      <div className="asset-preview">
        {hasImage
          ? <img src={`/api/v1/school-profile/${kind}?v=${profile.version}`} alt={text.imageAlt(label)} />
          : (
            <span className="asset-empty">
              <ImageOff aria-hidden="true" size={24} />
              <span>{text.noImage}</span>
            </span>
          )}
      </div>
      <p id={hintId} className="ui-field-hint">{text.imageHint(format.number(maxMegabytes))}</p>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} loading={reloading} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.fieldErrors.File ?? feedback.error} />
      <div className="form-actions">
        <FileUpload
          id={`${kind}-file`}
          label={hasImage ? text.replace : text.upload}
          accept="image/png,image/jpeg,image/webp"
          loading={upload.isPending}
          disabled={feedback.conflict}
          describedBy={hintId}
          onSelect={(file) => {
            feedback.reset();
            upload.mutate({ file, version: profile.version }, {
              onSuccess: () => feedback.showSuccess(text.uploaded),
              onError: feedback.showError,
            });
          }}
        />
        {hasImage && (
          <Button variant="ghost" icon={<Trash2 aria-hidden="true" size={20} />} onClick={() => setConfirmOpen(true)}>
            {text.remove}
          </Button>
        )}
      </div>
      <ConfirmDialog
        open={confirmOpen}
        danger
        title={text.removeTitle}
        consequence={text.removeConsequence}
        confirmLabel={text.remove}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        loading={remove.isPending}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => {
          feedback.reset();
          remove.mutate(profile.version, {
            onSuccess: () => feedback.showSuccess(text.removed),
            onError: feedback.showError,
            onSettled: () => setConfirmOpen(false),
          });
        }}
      />
    </section>
  );
}
