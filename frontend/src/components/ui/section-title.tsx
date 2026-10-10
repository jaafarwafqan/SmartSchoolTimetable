import type { LucideIcon } from "lucide-react";
import type { ReactNode, Ref } from "react";

type SectionTitleProps = {
  /** 2 for a section of a page, 3 for a sub-section. */
  level: 2 | 3;
  /** Required: every section title carries a lucide icon (DESIGN_SYSTEM.md, icons everywhere). */
  icon: LucideIcon;
  id?: string;
  className?: string;
  tabIndex?: number;
  ref?: Ref<HTMLHeadingElement>;
  children: ReactNode;
};

/** The only way features render an h2/h3: icon plus title text (enforced by lint and a test). */
export function SectionTitle({ level, icon: Icon, className = "", children, ...rest }: SectionTitleProps) {
  const Heading = level === 2 ? "h2" : "h3";
  return (
    <Heading {...rest} className={`ui-section-title ${className}`.trim()}>
      <Icon aria-hidden="true" size={level === 2 ? 22 : 18} />
      <span>{children}</span>
    </Heading>
  );
}
