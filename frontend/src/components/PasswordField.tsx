import { Eye, EyeOff } from "lucide-react";
import { useState } from "react";
import { messages } from "../i18n/messages";
import { passwordMinLength } from "../lib/credentialRules";
import { Input } from "./ui/input";

type PasswordFieldProps = {
  id: string;
  label: string;
  autoComplete: string;
  minLength?: number;
  required?: boolean;
};

export function PasswordField({
  id,
  label,
  autoComplete,
  minLength = passwordMinLength,
  required = true,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const toggleLabel = visible ? messages.app.hidePassword : messages.app.showPassword;
  const Icon = visible ? EyeOff : Eye;

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="password-control">
        <Input
          id={id}
          name={id}
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          minLength={minLength}
          required={required}
          dir="auto"
          className="password-input"
        />
        <button
          type="button"
          className="password-toggle"
          aria-label={toggleLabel}
          aria-pressed={visible}
          title={toggleLabel}
          onClick={() => setVisible((current) => !current)}
        >
          <Icon aria-hidden="true" size={20} strokeWidth={2} />
        </button>
      </div>
    </div>
  );
}
