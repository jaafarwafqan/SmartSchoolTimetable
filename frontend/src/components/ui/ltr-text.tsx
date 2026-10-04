import { ltrRuns } from "../../i18n/isolate";

const arabicLetter = /[؀-ۿ]/;

type LtrTextProps = { children: string; className?: string };

/**
 * Renders years, times, dates, codes, file names and usernames in their logical left-to-right order inside
 * RTL layouts (spec 2.5 §2.1). Pure LTR content is one `dir="ltr"` isolate; mixed Arabic text keeps its own
 * direction and only its numeric ranges and Latin runs are isolated.
 */
export function LtrText({ children, className }: LtrTextProps) {
  if (!arabicLetter.test(children)) {
    return <bdi dir="ltr" className={`ui-ltr ${className ?? ""}`.trim()}>{children}</bdi>;
  }
  return <bdi className={className}>{ltrRuns(children)}</bdi>;
}
