import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const styles = [
  { className: "type-h1", token: "h1 · text-2xl/3xl · 700", text: guideMessages.pageTitle },
  { className: "type-h2", token: "h2 · text-xl · 700", text: guideMessages.sectionTitle },
  { className: "type-h3", token: "h3 · text-lg · 600", text: guideMessages.subsectionTitle },
  { className: "type-body", token: "body · text-base · 400", text: guideMessages.bodyText },
  { className: "type-label", token: "label · text-sm · 600", text: guideMessages.labelText },
  { className: "type-caption", token: "caption · text-xs · 400", text: guideMessages.captionText },
] as const;

export function TypographySection() {
  return (
    <GuideSection id="guide-typography" title={guideMessages.typography}>
      <dl className="guide-type-list">
        {styles.map((style) => (
          <div key={style.className} className="guide-type-row">
            <dt className="guide-token-name font-mono" dir="ltr">{style.token}</dt>
            <dd className={style.className}>{style.text}</dd>
          </div>
        ))}
      </dl>
    </GuideSection>
  );
}
