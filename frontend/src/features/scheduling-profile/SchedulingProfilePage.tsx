import { RotateCcw, Save } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { Checkbox } from "../../components/ui/checkbox";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Select } from "../../components/ui/select";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { useRestoreDefaults, useSaveProfile, useSchedulingProfile, weightChoices, type RuleInput, type SchedulingProfile } from "./profileApi";

const text = messages.school.schedulingProfile;

function ProfileEditor({ profile, onReload }: { profile: SchedulingProfile; onReload: () => void }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSaveProfile();
  const restore = useRestoreDefaults();
  const [confirming, setConfirming] = useState(false);
  const toInputs = () => profile.rules.map((rule): RuleInput => ({ key: rule.key, enabled: rule.enabled, weight: rule.weight }));
  const [rules, setRules] = useSyncedState(toInputs, profile.version);
  const update = (index: number, change: Partial<RuleInput>) => setRules(rules.map((rule, at) => (at === index ? { ...rule, ...change } : rule)));

  return (
    <Card className="page-card" aria-label={text.title}>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <ul className="profile-rules" aria-label={text.title}>
        {profile.rules.map((rule, index) => {
          const name = text.rules[rule.key];
          const current = rules[index] ?? rule;
          return (
            <li key={rule.key} className="profile-rule">
              <Checkbox checked={current.enabled} onChange={(event) => update(index, { enabled: event.target.checked })}>{name}</Checkbox>
              <span className="profile-weight">
                <label htmlFor={`weight-${rule.key}`}>{text.weight}</label>
                <Select id={`weight-${rule.key}`} aria-label={text.weightOf(name)} value={String(current.weight)} disabled={!current.enabled}
                  options={weightChoices(current.weight).map((value) => ({ value: String(value), label: format.number(value) }))}
                  onChange={(event) => update(index, { weight: Number(event.target.value) })} />
                <span className="card-note">{text.defaultWeight(format.number(rule.defaultWeight))}</span>
              </span>
            </li>
          );
        })}
      </ul>
      <p className="card-note">
        {text.profileVersion(format.number(profile.profileVersion))}
        {profile.isDefault && <> <Badge tone="success">{text.isDefault}</Badge></>}
      </p>
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}
          onClick={() => {
            feedback.reset();
            save.mutate({ rules, version: profile.version }, { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError });
          }}>
          {text.save}
        </Button>
        <Button variant="secondary" icon={<RotateCcw aria-hidden="true" size={20} />} disabled={profile.isDefault} onClick={() => setConfirming(true)}>
          {text.restore}
        </Button>
      </div>
      <ConfirmDialog
        open={confirming}
        title={text.restoreTitle}
        consequence={text.restoreConsequence}
        confirmLabel={text.restore}
        confirmIcon={<RotateCcw aria-hidden="true" size={20} />}
        loading={restore.isPending}
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          feedback.reset();
          restore.mutate(profile.version, {
            onSuccess: (restored) => { setRules(restored.rules.map((rule) => ({ key: rule.key, enabled: rule.enabled, weight: rule.weight }))); feedback.showSuccess(text.restored); },
            onError: feedback.showError,
            onSettled: () => setConfirming(false),
          });
        }}
      />
    </Card>
  );
}

/** «ملف الجدولة» in Settings (Phase 3 §2.4): switch soft rules on or off and choose their weights. */
export function SchedulingProfilePage() {
  const profile = useSchedulingProfile();
  return (
    <div className="page">
      <PageHeader title={text.title} description={text.description} />
      {profile.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {profile.isPending && <Spinner label={messages.app.loadingContent} />}
      {profile.data && <ProfileEditor profile={profile.data} onReload={() => void profile.refetch()} />}
    </div>
  );
}
