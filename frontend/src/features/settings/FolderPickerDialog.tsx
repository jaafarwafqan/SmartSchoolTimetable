import { Check, ChevronUp, Folder, FolderOpen, HardDrive, Library, Monitor, X, type LucideIcon } from "lucide-react";
import { useState } from "react";
import { userErrorMessage } from "../../api";
import { Alert } from "../../components/ui/alert";
import { Button } from "../../components/ui/button";
import { Dialog } from "../../components/ui/dialog";
import { Spinner } from "../../components/ui/spinner";
import { isolate } from "../../i18n/isolate";
import { messages } from "../../i18n/messages";
import { useFolderListing, type FolderPlace } from "./backupApi";

const text = messages.school.backup.picker;

const placeIcons: Record<FolderPlace["kind"], LucideIcon> = { documents: Library, desktop: Monitor, drive: HardDrive };

type FolderPickerDialogProps = {
  open: boolean;
  /** The folder to open first (the current choice); the starting places when empty. */
  start: string;
  onChoose: (path: string) => void;
  onClose: () => void;
};

/**
 * MF10: choose a folder on this computer from the app itself (the browser cannot show a folder dialog of its own). The
 * server lists subfolders only, starting from documents, desktop and the drives; a typed path is the fallback elsewhere.
 */
export function FolderPickerDialog({ open, start, onChoose, onClose }: FolderPickerDialogProps) {
  const [path, setPath] = useState<string | null>(start.trim() === "" ? null : start);
  const listing = useFolderListing(path, open);
  // A folder that cannot be opened (removed or not allowed) drops back to the starting places instead of a dead end.
  const failed = listing.isError;
  const shown = listing.data;
  const placeName = (place: FolderPlace) => (place.kind === "drive" ? text.drive(isolate(place.name)) : text.places[place.kind]);

  return (
    <Dialog open={open} title={text.title} description={text.description} onClose={onClose}
      footer={(
        <>
          <Button icon={<Check aria-hidden="true" size={20} />} disabled={!shown?.path} onClick={() => shown?.path && onChoose(shown.path)}>{text.choose}</Button>
          <Button variant="secondary" icon={<X aria-hidden="true" size={20} />} onClick={onClose}>{messages.app.cancel}</Button>
        </>
      )}>
      <div className="form-stack">
        {failed && (
          <Alert tone="error" message={userErrorMessage(listing.error)}>
            <span>
              <Button size="sm" variant="secondary" icon={<FolderOpen aria-hidden="true" size={18} />} onClick={() => setPath(null)}>{text.toStart}</Button>
            </span>
          </Alert>
        )}
        <p className="folder-current">
          <FolderOpen aria-hidden="true" size={18} />
          {shown?.path ? <output className="folder-path" dir="ltr">{shown.path}</output> : <span>{text.thisComputer}</span>}
        </p>
        {listing.isPending && !failed && <Spinner label={messages.app.loadingContent} />}
        {shown?.path && (
          <Button variant="ghost" size="sm" icon={<ChevronUp aria-hidden="true" size={18} />} onClick={() => setPath(shown.parent)}>{text.up}</Button>
        )}
        {shown && (
          <ul className="folder-list" aria-label={text.listLabel}>
            {shown.places.map((place) => (
              <li key={place.path}>
                <Button variant="ghost" icon={(() => { const Icon = placeIcons[place.kind]; return <Icon aria-hidden="true" size={18} />; })()} onClick={() => setPath(place.path)}>
                  {placeName(place)}
                </Button>
              </li>
            ))}
            {shown.folders.map((folder) => (
              <li key={folder.path}>
                <Button variant="ghost" icon={<Folder aria-hidden="true" size={18} />} onClick={() => setPath(folder.path)}>{isolate(folder.name)}</Button>
              </li>
            ))}
          </ul>
        )}
        {shown?.path && shown.folders.length === 0 && <p className="card-note">{text.noSubfolders}</p>}
      </div>
    </Dialog>
  );
}
