import { RotateCcw, Save, CircleCheck, SlidersHorizontal, Repeat } from "lucide-react";
import { useState } from "react";
import { ConflictAlert } from "../../components/ConflictAlert";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Card } from "../../components/ui/card";
import { ChipGroup } from "../../components/ui/chip-group";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { SectionTitle } from "../../components/ui/section-title";
import { Spinner } from "../../components/ui/spinner";
import { messages } from "../../i18n/messages";
import { PageHeader } from "../../layout/PageHeader";
import { useFormatter } from "../../lib/schoolContext";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useSyncedState } from "../../lib/useSyncedState";
import { DailyCapCard } from "./DailyCapCard";
import { applyLevel, exclusiveRules, levelOf, priorityLevels, useRestoreDefaults, useSaveProfile, useSchedulingProfile, type RuleInput, type SchedulingProfile } from "./profileApi";

const text = messages.school.schedulingProfile;

function ProfileEditor({ profile, onReload }: { profile: SchedulingProfile; onReload: () => void }) {
  const format = useFormatter();
  const feedback = useFormFeedback();
  const save = useSaveProfile();
  const restore = useRestoreDefaults();
  const [confirming, setConfirming] = useState(false);
  const toInputs = () => profile.rules.map((rule): RuleInput => ({ key: rule.key, enabled: rule.enabled, weight: rule.weight }));
  const [rules, setRules] = useSyncedState(toInputs, profile.version);
  const [consecutive, setConsecutive] = useSyncedState(() => profile.defaultMaxConsecutive, profile.version);
  const [balanceHard, setBalanceHard] = useSyncedState(() => profile.balanceHard, profile.version);
  const balanceLevel = levelOf(rules.find((rule) => rule.key === "balanceTeacherDays") ?? { enabled: false, weight: 0 });

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
                {exclusiveRules[rule.key] && <span className="card-note">{text.exclusiveNote}</span>}
                {rule.key === "balanceTeacherDays" && (
                  <Checkbox checked={balanceHard && balanceLevel !== "notImportant"} disabled={balanceLevel === "notImportant"} onChange={(event) => setBalanceHard(event.target.checked)}>
                    {text.balanceHard}
                  </Checkbox>
                )}
                {rule.key === "balanceTeacherDays" && balanceHard && balanceLevel !== "notImportant" && <span className="card-note">{text.balanceHardHint}</span>}
              </span>
              <ChipGroup label={text.levelOf(name)} value={priorityLevels.indexOf(levelOf(current))}
                options={priorityLevels.map((level, rank) => ({ value: rank, label: text.levels[level] }))}
                onChange={(rank) => setRules(applyLevel(rules, rule.key, priorityLevels[rank]))} />
            </li>
          );
        })}
      </ul>
      <section className="form-stack" aria-labelledby="consecutive-title">
        <SectionTitle level={3} icon={Repeat} id="consecutive-title">{text.consecutive.title}</SectionTitle>
        <p className="card-note">{text.consecutive.description}</p>
        <Field id="default-consecutive" label={text.consecutive.label} hint={text.consecutive.hint}>
          <Select id="default-consecutive" value={String(consecutive)} options={[{ value: "2", label: text.consecutive.two }, { value: "3", label: text.consecutive.three }]}
            onChange={(event) => setConsecutive(Number(event.target.value))} />
        </Field>
      </section>
      <p className="card-note">
        {text.profileVersion(format.number(profile.profileVersion))}
        {profile.isDefault && <> <Badge tone="success" icon={<CircleCheck aria-hidden="true" size={16} />}>{text.isDefault}</Badge></>}
      </p>
      <div className="form-actions">
        <Button icon={<Save aria-hidden="true" size={20} />} loading={save.isPending} disabled={feedback.conflict}
          onClick={() => {
            feedback.reset();
            save.mutate({ rules, version: profile.version, defaultMaxConsecutive: consecutive, balanceHard: balanceHard && balanceLevel !== "notImportant" }, { onSuccess: () => feedback.showSuccess(text.saved), onError: feedback.showError });
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
            onSuccess: (restored) => {
              setRules(restored.rules.map((rule) => ({ key: rule.key, enabled: rule.enabled, weight: rule.weight })));
              setConsecutive(restored.defaultMaxConsecutive);
              setBalanceHard(restored.balanceHard);
              feedback.showSuccess(text.restored);
            },
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
      <DailyCapCard />
    </div>
  );
}
