import { ChevronLeft, ChevronRight } from "lucide-react";
import { messages } from "../../i18n/messages";
import type { Formatter } from "../../lib/format";
import { Button } from "./button";

type PaginationProps = {
  page: number;
  pageSize: number;
  total: number;
  format: Formatter;
  onPage: (page: number) => void;
};

/** Previous/next paging with a live "page X of Y" summary. Arrows follow RTL reading direction. */
export function Pagination({ page, pageSize, total, format, onPage }: PaginationProps) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (total <= pageSize && page === 1) return null;
  return (
    <nav className="ui-pagination" aria-label={messages.school.common.pageOf(format.number(page), format.number(pages))}>
      <Button size="sm" variant="secondary" icon={<ChevronRight aria-hidden="true" size={18} />} disabled={page <= 1} onClick={() => onPage(page - 1)}>
        {messages.school.common.previousPage}
      </Button>
      <span className="type-caption" aria-live="polite">
        {messages.school.common.pageOf(format.number(page), format.number(pages))}
      </span>
      <Button size="sm" variant="secondary" icon={<ChevronLeft aria-hidden="true" size={18} />} disabled={page >= pages} onClick={() => onPage(page + 1)}>
        {messages.school.common.nextPage}
      </Button>
    </nav>
  );
}
