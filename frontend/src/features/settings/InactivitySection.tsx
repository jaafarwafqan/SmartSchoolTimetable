import { Clock, Save } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { useMemo, type FormEvent } from "react";
import { apiRequest } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Field } from "../../components/ui/field";
import { Select, type SelectOption } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import type { Bootstrap } from "../../lib/bootstrapQuery";
import { formatInactivityTimeout } from "../../lib/format";
import { useFormFeedback } from "../../lib/useFormFeedback";
import { useRefreshBootstrap } from "../auth/useBootstrap";
import { SettingsSection } from "./SettingsSection";

const neverValue = "never";
const fieldId = "inactivityTimeout";

function toValue(minutes: number | null): string {
  return minutes === null ? neverValue : String(minutes);
}

/** Inactivity auto-lock choice: persisted per owner, applied by the server without a restart. */
export function InactivitySection({ bootstrap }: { bootstrap: Bootstrap }) {
  const refreshBootstrap = useRefreshBootstrap();
  const feedback = useFormFeedback();
  const current = bootstrap.inactivityTimeoutMinutes;

  const options = useMemo<SelectOption[]>(() => {
    const minutes = [...bootstrap.inactivityTimeoutChoices];
    // A configured default outside the offered choices stays visible as the current value.
    if (current !== null && !minutes.includes(current)) minutes.push(current);
    return [
      ...minutes.sort((a, b) => a - b).map((value) => ({ value: String(value), label: formatInactivityTimeout(value) })),
      { value: neverValue, label: formatInactivityTimeout(null) },
    ];
  }, [bootstrap.inactivityTimeoutChoices, current]);

  const save = useMutation({
    mutationFn: (inactivityTimeout: string) =>
      apiRequest<{ inactivityTimeoutMinutes: number | null }>(
        "/api/v1/settings/inactivity-timeout",
        "PUT",
        { inactivityTimeout },
      ),
    onSuccess: async () => {
      await refreshBootstrap();
      feedback.showSuccess(messages.app.inactivitySaved);
    },
    onError: feedback.showError,
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    feedback.reset();
    save.mutate(String(new FormData(event.currentTarget).get(fieldId) ?? ""));
  }

  return (
    <SettingsSection
      id="session-section"
      icon={<Clock size={20} strokeWidth={2} />}
      title={messages.app.sessionSectionTitle}
      description={messages.app.sessionSectionDescription}
    >
      <dl className="info-row">
        <dt>{messages.app.inactivityCurrent}</dt>
        <dd><Badge tone="primary" icon={<Clock aria-hidden="true" size={16} />}>{formatInactivityTimeout(current)}</Badge></dd>
      </dl>
      <Alert tone="success" message={feedback.success} />
      <Alert tone="error" message={feedback.error} />
      <form ref={feedback.formRef} className="form-stack" noValidate onSubmit={submit} onInput={feedback.clearFieldFromEvent}>
        <Field id={fieldId} label={messages.app.inactivityLabel} error={feedback.fieldErrors.InactivityTimeout}>
          <Select
            id={fieldId}
            name={fieldId}
            key={toValue(current)}
            defaultValue={toValue(current)}
            options={options}
            data-field="InactivityTimeout"
            aria-invalid={feedback.fieldErrors.InactivityTimeout ? true : undefined}
          />
        </Field>
        <div className="form-actions">
          <Button type="submit" icon={<Save aria-hidden="true" size={20} />} loading={save.isPending}>
            {messages.app.saveInactivity}
          </Button>
        </div>
      </form>
    </SettingsSection>
  );
}
