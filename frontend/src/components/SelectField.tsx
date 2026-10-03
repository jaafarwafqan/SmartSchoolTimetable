import type { FieldName } from "../i18n/messages";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { Select, type SelectOption } from "./ui/select";

type SelectFieldProps = {
  id: string;
  label: string;
  options: readonly SelectOption[];
  defaultValue?: string;
  required?: boolean;
  hint?: string;
  field?: FieldName;
  errors?: FieldErrors;
};

export function SelectField({ id, label, options, defaultValue, required, hint, field, errors }: SelectFieldProps) {
  const error = field ? errors?.[field] : undefined;
  return (
    <Field id={id} label={label} required={required} hint={hint} error={error}>
      <Select
        id={id}
        name={id}
        options={options}
        defaultValue={defaultValue}
        required={required}
        data-field={field}
        aria-invalid={error ? true : undefined}
        aria-describedby={fieldDescribedBy(id, hint, error)}
      />
    </Field>
  );
}
