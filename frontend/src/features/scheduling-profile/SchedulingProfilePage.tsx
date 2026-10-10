import { RotateCcw, Save, CircleCheck, SlidersHorizontal } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ChipGroup } from "../../components/ui/chip-group";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { levelOf, priorityLevels, useRestoreDefaults, useSaveProfile, useSchedulingProfile, withLevel, type RuleInput, type SchedulingProfile } from "./profileApi";

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
    <Card className="page-card" aria-labelledby="priorities-title">
      <SectionTitle level={2} icon={SlidersHorizontal} id="priorities-title">{text.title}</SectionTitle>
      <p className="card-note">{text.description}</p>
      {feedback.conflict && <ConflictAlert onReload={() => { feedback.reset(); onReload(); }} />}
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <ul className="profile-rules" aria-label={text.title}>
        {profile.rules.map((rule, index) => {
          const name = text.rules[rule.key];
          const current = rules[index] ?? rule;
          return (
            <li key={rule.key} className="profile-rule">
              <span className="profile-rule-text">
                <strong>{name}</strong>
                <span className="card-note">{text.hints[rule.key]}</span>
              </span>
              <ChipGroup label={text.levelOf(name)} value={priorityLevels.indexOf(levelOf(current))}
                options={priorityLevels.map((level, rank) => ({ value: rank, label: text.levels[level] }))}
                onChange={(rank) => update(index, withLevel(current, priorityLevels[rank]))} />
            </li>
          );
        })}
      </ul>
      <p className="card-note">
        {text.profileVersion(format.number(profile.profileVersion))}
        {profile.isDefault && <> <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.isDefault}</Badge></>}
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

/** «أولويات الجدول» under Settings › «متقدم» (MF9): how much each soft rule matters, in three plain levels. */
export function SchedulingProfilePage() {
  const profile = useSchedulingProfile();
  return (
    <div className="page">
      <PageHeader icon={SlidersHorizontal} title={messages.school.nav.settingsAdvanced} description={messages.school.nav.settingsAdvancedDescription} />
      {profile.isError && <Alert tone="error" message={messages.school.common.loadFailed} />}
      {profile.isPending && <Spinner label={messages.app.loadingContent} />}
      {profile.data && <ProfileEditor profile={profile.data} onReload={() => void profile.refetch()} />}
    </div>
  );
}
