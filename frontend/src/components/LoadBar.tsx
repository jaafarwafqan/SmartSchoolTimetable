import { CircleAlert, CircleCheck, TriangleAlert } from "lucide-react";
import { messages } from "../i18n/messages";
import { Badge } from "./ui/badge";

export type LoadStatus = "within" | "near" | "over";

const text = messages.school.workload;
const icons = { within: CircleCheck, near: TriangleAlert, over: CircleAlert } as const;
const tones = { within: "success", near: "warning", over: "danger" } as const;

/** «ضمن الحد» / «قريب» / «تجاوز» with an icon and text, never colour alone. */
export function LoadStatusBadge({ status }: { status: LoadStatus }) {
  const Icon = icons[status];
  return <Badge tone={tones[status]} icon={<Icon aria-hidden="true" size={16} />}>{text.status[status]}</Badge>;
}

type LoadBarProps = {
  /** Accessible name, for example «نصاب أحمد: ١٠ من ١٢». */
  label: string;
  assigned: number;
  limit: number;
  status: LoadStatus;
};

/** Width of the filled part of a load bar, 0–100 (an overload fills the bar). */
export function loadPercent(assigned: number, limit: number): number {
  if (limit <= 0) return assigned > 0 ? 100 : 0;
  return Math.min(100, Math.round((assigned / limit) * 100));
}

/** A teacher's assigned lessons against their limit (DESIGN_SYSTEM.md: load bar). An overload fills the bar. */
export function LoadBar({ label, assigned, limit, status }: LoadBarProps) {
  const percent = loadPercent(assigned, limit);
  return (
    <span className={`load-bar is-${status}`} role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={Math.max(limit, assigned)} aria-valuenow={assigned}>
      <span className="load-bar-fill" style={{ inlineSize: `${percent}%` }} />
    </span>
  );
}
