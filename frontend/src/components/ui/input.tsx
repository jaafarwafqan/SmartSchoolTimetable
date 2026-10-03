import type { InputHTMLAttributes } from "react";

export function Input({ className = "", ...props }: InputHTMLAttributes<HTMLInputElement>) {
  // The base class must be merged after spreading props; a caller's className must not replace it.
  return <input {...props} className={`ui-input ${className}`.trim()} />;
}
