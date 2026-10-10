import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type FolderEntry = { name: string; path: string };
export type FolderPlace = { kind: "documents" | "desktop" | "drive"; name: string; path: string };
/** One folder for the picker: its subfolders, the folder above, or (path null) the starting places. */
export type FolderListing = { path: string | null; parent: string | null; folders: FolderEntry[]; places: FolderPlace[] };

export type BackupKind = "manual" | "preRestore" | "preConversion" | "other";
export type BackupFile = {
  name: string;
  path: string;
  sizeBytes: number;
  modifiedAt: string;
  kind: BackupKind;
  source: "folder" | "automatic";
  restorable: boolean;
};
export type BackupListing = { folder: string; automaticFolder: string; files: BackupFile[] };

const backupPath = "/api/v1/backup";

export function useFolderListing(path: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["backup", "folders", path],
    queryFn: () => apiRequest<FolderListing>(`${backupPath}/folders${path ? `?path=${encodeURIComponent(path)}` : ""}`),
    enabled,
  });
}

/** The backups to restore from: database files of the chosen folder plus the automatic ones next to the database. */
export function useBackupFiles(folder: string) {
  return useQuery({
    queryKey: ["backup", "files", folder],
    queryFn: () => apiRequest<BackupListing>(`${backupPath}/files?folder=${encodeURIComponent(folder)}`),
    enabled: folder.trim() !== "",
    refetchOnMount: "always",
  });
}

/** 1 MB = 1,048,576 bytes: whole megabytes from one megabyte up, whole kilobytes below (never zero). */
export function sizeParts(bytes: number): { unit: "kb" | "mb"; value: number } {
  return bytes >= 1_048_576 ? { unit: "mb", value: Math.round(bytes / 1_048_576) } : { unit: "kb", value: Math.max(1, Math.round(bytes / 1024)) };
}
