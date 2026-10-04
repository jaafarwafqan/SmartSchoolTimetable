import { useState } from "react";

/**
 * Local edit state seeded from server data. When the stored `version` changes (a save here, or a reload after a
 * conflict) and there are no unsaved edits, the state is re-seeded from `source`. This keeps the component mounted,
 * so mutation callbacks still run; remounting with a version key would drop them.
 */
export function useSyncedState<T>(source: () => T, version: number, dirty = false) {
  const [value, setValue] = useState<T>(source);
  const [syncedVersion, setSyncedVersion] = useState(version);
  if (version !== syncedVersion && !dirty) {
    setSyncedVersion(version);
    setValue(source());
  }
  return [value, setValue] as const;
}
