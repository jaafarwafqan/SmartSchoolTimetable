import { Search } from "lucide-react";
import { useEffect, useState } from "react";
import { Field } from "./ui/field";
import { Input } from "./ui/input";

type SearchFieldProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  /** Debounce before notifying, so typing does not send a request per keystroke. */
  delayMs?: number;
};

/** List search box (the server normalizes Arabic spelling variants). */
export function SearchField({ id, label, value, onChange, delayMs = 250 }: SearchFieldProps) {
  const [draft, setDraft] = useState(value);
  useEffect(() => {
    if (draft === value) return;
    const timeout = window.setTimeout(() => onChange(draft), delayMs);
    return () => window.clearTimeout(timeout);
  }, [draft, value, onChange, delayMs]);

  return (
    <Field id={id} label={label}>
      <span className="ui-search-control">
        <Search className="ui-search-icon" aria-hidden="true" size={18} />
        <Input id={id} type="search" className="ui-search-input" value={draft} onChange={(event) => setDraft(event.currentTarget.value)} />
      </span>
    </Field>
  );
}
