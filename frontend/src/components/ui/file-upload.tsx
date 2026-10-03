import { Upload } from "lucide-react";
import { useRef } from "react";
import { Button } from "./button";

type FileUploadProps = {
  id: string;
  label: string;
  accept: string;
  loading?: boolean;
  disabled?: boolean;
  /** Ids of the hint/error elements describing the control. */
  describedBy?: string;
  onSelect: (file: File) => void;
};

/**
 * File picker with an app button instead of the browser's native file-input text (CLAUDE.md).
 * The native input stays in the DOM, visually hidden, so it remains reachable for assistive technology.
 */
export function FileUpload({ id, label, accept, loading = false, disabled = false, describedBy, onSelect }: FileUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  return (
    <span className="ui-file-upload">
      <input
        ref={inputRef}
        id={id}
        type="file"
        accept={accept}
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        disabled={disabled || loading}
        onChange={(event) => {
          const file = event.currentTarget.files?.[0];
          event.currentTarget.value = "";
          if (file) onSelect(file);
        }}
      />
      <Button
        variant="secondary"
        icon={<Upload aria-hidden="true" size={20} />}
        loading={loading}
        disabled={disabled}
        aria-describedby={describedBy}
        onClick={() => inputRef.current?.click()}
      >
        {label}
      </Button>
    </span>
  );
}
