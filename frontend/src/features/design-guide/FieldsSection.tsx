import { PasswordField } from "../../components/PasswordField";
import { TextField } from "../../components/TextField";
import { Checkbox } from "../../components/ui/checkbox";
import { Field } from "../../components/ui/field";
import { Select } from "../../components/ui/select";
import { messages } from "../../i18n/messages";
import { formatInactivityTimeout } from "../../lib/format";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const selectOptions = [15, 30, 60].map((minutes) => ({ value: String(minutes), label: formatInactivityTimeout(minutes) }));

export function FieldsSection() {
  return (
    <GuideSection id="guide-fields" title={guideMessages.fields}>
      <div className="guide-grid">
        <TextField id="guide-field-normal" label={`${guideMessages.sampleFieldLabel} · ${guideMessages.stateNormal}`} hint={guideMessages.sampleFieldHint} required />
        <TextField id="guide-field-focus" label={`${guideMessages.sampleFieldLabel} · ${guideMessages.stateFocus}`} data-state="focus" />
        <TextField
          id="guide-field-error"
          label={`${guideMessages.sampleFieldLabel} · ${guideMessages.stateError}`}
          field="Username"
          errors={{ Username: guideMessages.sampleFieldError }}
          required
        />
        <TextField id="guide-field-disabled" label={`${guideMessages.sampleFieldLabel} · ${guideMessages.stateDisabled}`} disabled />
        <PasswordField id="guide-field-password" label={messages.app.password} autoComplete="off" />
        <Field id="guide-field-select" label={messages.app.inactivityLabel}>
          <Select id="guide-field-select" options={selectOptions} defaultValue="30" />
        </Field>
        <Checkbox defaultChecked>{messages.app.confirmCodeSaved}</Checkbox>
      </div>
    </GuideSection>
  );
}
