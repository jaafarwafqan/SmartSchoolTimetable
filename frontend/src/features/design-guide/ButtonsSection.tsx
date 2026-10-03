import { Plus, Save, Trash2 } from "lucide-react";
import { Button } from "../../components/ui/button";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

const variants = [
  { variant: "primary", label: guideMessages.variantPrimary, action: guideMessages.actionSave, Icon: Save },
  { variant: "secondary", label: guideMessages.variantSecondary, action: guideMessages.actionAdd, Icon: Plus },
  { variant: "ghost", label: guideMessages.variantGhost, action: guideMessages.actionAdd, Icon: Plus },
  { variant: "danger", label: guideMessages.variantDanger, action: guideMessages.actionDelete, Icon: Trash2 },
] as const;

const states = [
  { key: "normal", label: guideMessages.stateNormal },
  { key: "hover", label: guideMessages.stateHover },
  { key: "focus", label: guideMessages.stateFocus },
  { key: "disabled", label: guideMessages.stateDisabled },
  { key: "loading", label: guideMessages.stateLoading },
] as const;

const sizes = [
  { size: "sm", label: guideMessages.sizeSmall },
  { size: "md", label: guideMessages.sizeMedium },
  { size: "lg", label: guideMessages.sizeLarge },
] as const;

/** Hover and focus are forced with data-state so every state is visible at once. */
export function ButtonsSection() {
  return (
    <GuideSection id="guide-buttons" title={guideMessages.buttons}>
      <div className="guide-matrix">
        {variants.map(({ variant, label, action, Icon }) => (
          <div key={variant} className="guide-matrix-row">
            <span className="type-label">{label}</span>
            {states.map((state) => (
              <span key={state.key} className="guide-matrix-cell">
                <Button
                  variant={variant}
                  icon={<Icon aria-hidden="true" size={20} />}
                  data-state={state.key === "hover" || state.key === "focus" ? state.key : undefined}
                  disabled={state.key === "disabled"}
                  loading={state.key === "loading"}
                >
                  {action}
                </Button>
                <span className="type-caption">{state.label}</span>
              </span>
            ))}
          </div>
        ))}
      </div>
      <div className="guide-row">
        {sizes.map(({ size, label }) => (
          <Button key={size} size={size} variant="secondary" icon={<Plus aria-hidden="true" size={20} />}>
            {label}
          </Button>
        ))}
      </div>
    </GuideSection>
  );
}
