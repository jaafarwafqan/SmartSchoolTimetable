import { MessageSquareWarning, Trash2 } from "lucide-react";
import { useState } from "react";
import { Button } from "../../components/ui/button";
import { ConfirmDialog } from "../../components/ui/confirm-dialog";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";

export function DialogSection() {
  const [open, setOpen] = useState(false);
  return (
    <GuideSection id="guide-dialog" title={guideMessages.dialog}>
      <div className="guide-row">
        <Button variant="secondary" icon={<MessageSquareWarning aria-hidden="true" size={20} />} onClick={() => setOpen(true)}>
          {guideMessages.openDialog}
        </Button>
      </div>
      <ConfirmDialog
        open={open}
        danger
        title={guideMessages.dialogTitle}
        consequence={guideMessages.dialogBody}
        confirmLabel={guideMessages.actionDelete}
        confirmIcon={<Trash2 aria-hidden="true" size={20} />}
        onConfirm={() => setOpen(false)}
        onCancel={() => setOpen(false)}
      />
    </GuideSection>
  );
}
