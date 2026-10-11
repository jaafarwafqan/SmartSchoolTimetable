import { mkdirSync } from "node:fs";

/** Creates (and returns) the folder where a printing spec leaves its PDFs, so they can be rendered to images and looked at. */
export function multiStageSchoolPdfFolder(folder: string): string {
  mkdirSync(folder, { recursive: true });
  return folder;
}
