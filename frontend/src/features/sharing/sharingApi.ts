import { useMutation, useQuery } from "@tanstack/react-query";
import { apiDownload, apiRequest } from "../../api";
import { queryClient } from "../../lib/queryClient";

/** ADR 0056: the school key, the approved version and its teachers (`GET /api/v1/sharing/`). */
export type SchoolKey = { schoolId: string; fingerprint: string; generation: number; createdAt: string; readable: boolean; backedUpAt: string | null };
export type ApprovedTimetable = { id: number; number: number; approvedAt: string; conflicts: boolean; expiresOn: string; yearEnded: boolean };
export type ShareTeacher = { id: number; name: string; lessons: number };
export type SharingStatus = { key: SchoolKey | null; approved: ApprovedTimetable | null; teachers: ShareTeacher[] };

/** A QR's modules: one string of "0"/"1" per row, quiet zone included. */
export type QrCode = { version: number; size: number; rows: string[] };
/** `qr` is null when the package is too large for one reliable code (send the .sst file instead). */
export type PackageQr = { qr: QrCode | null; characters: number };
export type PairingQr = { schoolName: string; fingerprint: string; code: PackageQr };
export type TeacherQr = { teacherId: number; name: string; lessons: number; versionNumber: number; approvedAt: string; code: PackageQr };
export type TeacherCard = { teacherId: number; name: string; lessons: number; code: PackageQr };
export type TeacherCards = {
  schoolName: string; academicYear: string; versionNumber: number; approvedAt: string; fingerprint: string; pairing: PackageQr;
  appLink: string | null; appQr: QrCode | null; cards: TeacherCard[];
};
export type SharingSettings = { appLink: string | null; version: number };
export type ImportKeyResult = { key: SchoolKey; changed: boolean };

export const sharingKey = ["sharing"] as const;

export function useSharingStatus() {
  return useQuery({ queryKey: sharingKey, queryFn: () => apiRequest<SharingStatus>("/api/v1/sharing/") });
}

export function useSharingSettings() {
  return useQuery({ queryKey: [...sharingKey, "settings"], queryFn: () => apiRequest<SharingSettings>("/api/v1/sharing/settings") });
}

const refresh = () => queryClient.invalidateQueries({ queryKey: sharingKey });

/** Every export can make the school key (first use), so the status is read again afterwards. */
export function useDownload() {
  return useMutation({
    mutationFn: ({ path, body, fallback }: { path: string; body?: unknown; fallback: string }) => apiDownload(path, body ?? {}, fallback),
    onSettled: refresh,
  });
}

export function usePairingQr() {
  return useMutation({ mutationFn: () => apiRequest<PairingQr>("/api/v1/sharing/pairing-qr", "POST"), onSettled: refresh });
}

export function useTeacherQr() {
  return useMutation({ mutationFn: (teacherId: number) => apiRequest<TeacherQr>(`/api/v1/sharing/teachers/${teacherId}/qr`, "POST"), onSettled: refresh });
}

export function useTeacherCards() {
  return useMutation({ mutationFn: (teacherIds: number[] | null) => apiRequest<TeacherCards>("/api/v1/sharing/cards", "POST", { teacherIds }), onSettled: refresh });
}

export function useSaveSharingSettings() {
  return useMutation({
    mutationFn: (settings: { appLink: string | null; version: number }) => apiRequest<SharingSettings>("/api/v1/sharing/settings", "PUT", settings),
    onSuccess: (saved) => queryClient.setQueryData([...sharingKey, "settings"], saved),
  });
}

export function useImportKey() {
  return useMutation({
    mutationFn: (command: { file: string; password: string; confirmReplace: boolean }) => apiRequest<ImportKeyResult>("/api/v1/sharing/key/import", "POST", command),
    onSuccess: refresh,
  });
}

export function useRegenerateKey() {
  return useMutation({
    mutationFn: () => apiRequest<SchoolKey>("/api/v1/sharing/key/regenerate", "POST", { confirm: true, confirmRepair: true }),
    onSuccess: refresh,
  });
}

export function useKiosk() {
  return {
    start: useMutation({ mutationFn: () => apiRequest<{ key: string }>("/api/v1/sharing/kiosk/start", "POST") }),
    stop: useMutation({ mutationFn: () => apiRequest<void>("/api/v1/sharing/kiosk/stop", "POST") }),
  };
}

/** A file's bytes as base64 (the key copy is about 200 bytes). */
export async function fileToBase64(file: File): Promise<string> {
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}
