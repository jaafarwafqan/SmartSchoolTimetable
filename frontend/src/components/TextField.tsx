import type { InputHTMLAttributes } from "react";
import type { FieldName } from "../i18n/messages";
import type { FieldErrors } from "../lib/useFormFeedback";
import { Field, fieldDescribedBy } from "./ui/field";
import { Input } from "./ui/input";

type TextFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "id"> & {
  id: string;
  label: string;
  hint?: string;
  /** API/validation field name; links the input to its entry in `errors` and clears it on input. */
  field?: FieldName;
  errors?: FieldErrors;
};

export function TextField({ id, label, hint, field, errors, required, ...inputProps }: TextFieldProps) {
  const error = field ? errors?.[field] : undefined;
  return (
    <Field id={id} label={label} required={required} hint={hint} error={error}>
      <Input
        {...inputProps}
        id={id}
        name={inputProps.name ?? id}
        required={required}
        data-field={field}
        aria-invalid={error ? true : undefined}
        aria-describedby={fieldDescribedBy(id, hint, error)}
      />
    </Field>
  );
}
