import { LoaderCircle } from "lucide-react";

type SpinnerProps = {
  /** Screen-reader text; omit when a visible label already describes the loading state. */
  label?: string;
  size?: number;
};

export function Spinner({ label, size = 20 }: SpinnerProps) {
  return (
    <span className="ui-spinner" role={label ? "status" : undefined}>
      <LoaderCircle className="ui-spin" aria-hidden="true" size={size} strokeWidth={2} />
      {label && <span className="sr-only">{label}</span>}
    </span>
  );
}
