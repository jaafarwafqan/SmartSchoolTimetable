import { useEffect, useState } from "react";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";
import { colorTokens } from "./sampleData";

/** Every colour token with its live value read from the computed CSS (tokens.css is the only source). */
export function ColorTokensSection() {
  const [values, setValues] = useState<Record<string, string>>({});

  useEffect(() => {
    const style = getComputedStyle(document.documentElement);
    setValues(Object.fromEntries(colorTokens.map((token) =>
      [token.name, style.getPropertyValue(`--color-${token.name}`).trim()])));
  }, []);

  return (
    <GuideSection id="guide-colors" title={guideMessages.colors}>
      <ul className="guide-swatches">
        {colorTokens.map((token) => (
          <li key={token.name} className="guide-swatch">
            <span className={`guide-swatch-color ${token.swatchClass}`} aria-hidden="true" />
            <span className="guide-token-name font-mono" dir="ltr">{token.name}</span>
            <span className="guide-token-value font-mono" dir="ltr">{values[token.name]}</span>
          </li>
        ))}
      </ul>
    </GuideSection>
  );
}
