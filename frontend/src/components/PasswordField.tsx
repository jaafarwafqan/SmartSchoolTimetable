import { Eye, EyeOff } from "lucide-react";
import { useState } from "react";
import { messages, type FieldName } from "../i18n/messages";
import { passwordMinLength } from "../lib/credentialRules";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { IconButton } from "./ui/icon-button";
import { Input } from "./ui/input";

type PasswordFieldProps = {
  id: string;
  label: string;
  autoComplete: string;
  minLength?: number;
  required?: boolean;
  hint?: string;
  /** API/validation field name; links the input to its entry in `errors` and clears it on input. */
  field?: FieldName;
  errors?: FieldErrors;
};

export function PasswordField({
  id,
  label,
  autoComplete,
  minLength = passwordMinLength,
  required = true,
  hint,
  field,
  errors,
}: PasswordFieldProps) {
  const error = field ? errors?.[field] : undefined;
  const [visible, setVisible] = useState(false);
  const toggleLabel = visible ? messages.app.hidePassword : messages.app.showPassword;
  const Icon = visible ? EyeOff : Eye;

  return (
    <Field id={id} label={label} required={required} hint={hint} error={error}>
      {/* The eye control sits inside the field at its logical end (left in RTL). */}
      <div className="ui-password-control">
        <Input
          id={id}
          name={id}
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          minLength={minLength}
          required={required}
          className="ui-password-input"
          data-field={field}
          aria-invalid={error ? true : undefined}
          aria-describedby={fieldDescribedBy(id, hint, error)}
        />
        <IconButton
          className="ui-password-toggle"
          aria-label={toggleLabel}
          aria-pressed={visible}
          aria-controls={id}
          title={toggleLabel}
          icon={<Icon aria-hidden="true" size={20} strokeWidth={2} />}
          onClick={() => setVisible((current) => !current)}
        />
      </div>
    </Field>
  );
}
