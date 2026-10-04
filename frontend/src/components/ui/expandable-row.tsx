import { ChevronDown } from "lucide-react";
import type { ReactNode } from "react";

type ExpandableRowProps = {
  id: string;
  /** Visible summary; also the accessible name of the toggle. */
  summary: ReactNode;
  /** Row actions (archive, delete …) next to the toggle. */
  actions?: ReactNode;
  expanded: boolean;
  onToggle: () => void;
  children: ReactNode;
};

/**
 * Add pattern "details in place" (DESIGN_SYSTEM.md 14): a list row whose details open inside the list,
 * never in a modal or a side panel. The toggle is a real button with aria-expanded and aria-controls.
 */
export function ExpandableRow({ id, summary, actions, expanded, onToggle, children }: ExpandableRowProps) {
  const regionId = `${id}-details`;
  return (
    <li className={`ui-expandable${expanded ? " is-expanded" : ""}`}>
      <div className="ui-expandable-header">
        <button type="button" className="ui-expandable-toggle" aria-expanded={expanded} aria-controls={regionId} onClick={onToggle}>
          <ChevronDown aria-hidden="true" size={20} className="ui-expandable-chevron" />
          <span className="ui-expandable-summary">{summary}</span>
        </button>
        {actions && <div className="ui-expandable-actions">{actions}</div>}
      </div>
      {expanded && <div id={regionId} className="ui-expandable-body">{children}</div>}
    </li>
  );
}
