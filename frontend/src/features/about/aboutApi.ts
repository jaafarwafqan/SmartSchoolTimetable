import { useQuery } from "@tanstack/react-query";
import { apiRequest } from "../../api";

export type ThirdPartyLicense = { name: string; license: string };

export type About = {
  version: string;
  /** yyyy-MM-dd (UTC day of the build). */
  buildDate: string;
  schemaVersion: number;
  /** The licence notice: the Arabic text, a divider line, then the English text. */
  license: string;
  dataFolder: string;
  thirdParty: ThirdPartyLicense[];
};

export const aboutKey = ["about"] as const;

/** Fixed for the life of the running program, so it is fetched once. */
export function useAbout() {
  return useQuery({ queryKey: aboutKey, queryFn: () => apiRequest<About>("/api/v1/about/"), staleTime: Infinity });
}

/** The Arabic part of the licence (before the divider line). */
export function arabicLicense(license: string): string {
  return (license.split(/^-{5,}\s*$/m)[0] ?? "").trim();
}
