import { Plus } from "lucide-react";
import type { FormEvent, ReactNode, RefObject } from "react";
import { Button } from "./ui/button";

type InlineAddFormProps = {
  /** Accessible name of the row (e.g. "إضافة مادة"). */
  label: string;
  buttonLabel: string;
  pending: boolean;
  formRef?: RefObject<HTMLFormElement | null>;
  onSubmit: (form: FormData, element: HTMLFormElement) => void;
  onInput?: (event: FormEvent<HTMLFormElement>) => void;
  children: ReactNode;
};

/**
 * Add pattern "inline row" (DESIGN_SYSTEM.md 14): one or two fields in the list itself; Enter submits.
 * The caller clears the fields after a successful add so the next item can be typed straight away.
 */
export function InlineAddForm({ label, buttonLabel, pending, formRef, onSubmit, onInput, children }: InlineAddFormProps) {
  return (
    <form
      ref={formRef}
      className="inline-add"
      aria-label={label}
      noValidate
      onInput={onInput}
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit(new FormData(event.currentTarget), event.currentTarget);
      }}
    >
      <div className="inline-add-fields">{children}</div>
      <Button type="submit" icon={<Plus aria-hidden="true" size={20} />} loading={pending}>{buttonLabel}</Button>
    </form>
  );
}
