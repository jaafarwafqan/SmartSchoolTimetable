import { ChevronLeft } from "lucide-react";
import { Link } from "react-router-dom";

export type Crumb = { label: string; to?: string };

/** Breadcrumb trail; the last item is the current page (aria-current="page"). The separator mirrors in RTL. */
export function Breadcrumbs({ label, items }: { label: string; items: readonly Crumb[] }) {
  return (
    <nav aria-label={label} className="ui-breadcrumbs">
      <ol>
        {items.map((item, index) => {
          const last = index === items.length - 1;
          return (
            <li key={`${item.label}-${index}`}>
              {last || !item.to
                ? <span aria-current={last ? "page" : undefined}>{item.label}</span>
                : <Link to={item.to}>{item.label}</Link>}
              {!last && <ChevronLeft aria-hidden="true" size={16} strokeWidth={2} />}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
