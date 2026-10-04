import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { useRefreshSchoolData } from "../../lib/schoolContext";

export type SchoolProfile = {
  name: string;
  schoolType: string;
  studyType: string;
  principalName: string | null;
  scheduleOfficerName: string | null;
  timeZone: string;
  numeralSystem: string;
  calendarDisplay: string;
  hasLogo: boolean;
  hasStamp: boolean;
  version: number;
  options: {
    schoolTypes: string[];
    studyTypes: string[];
    numeralSystems: string[];
    calendarDisplays: string[];
    timeZones: string[];
  };
};

export type SchoolProfileInput = Omit<SchoolProfile, "hasLogo" | "hasStamp" | "options">;
export type AssetKind = "logo" | "stamp";

export const profileKey = ["school-profile"] as const;
const profilePath = "/api/v1/school-profile";

export function useSchoolProfile() {
  return useQuery({ queryKey: profileKey, queryFn: () => apiRequest<SchoolProfile>(profilePath) });
}

/** Every successful profile change refreshes the profile, the app shell context and the dashboard. */
function useProfileMutation<TInput>(request: (input: TInput) => Promise<SchoolProfile>) {
  const queryClient = useQueryClient();
  const refreshSchoolData = useRefreshSchoolData();
  return useMutation({
    mutationFn: request,
    onSuccess: async (profile) => {
      queryClient.setQueryData(profileKey, profile);
      await refreshSchoolData();
    },
  });
}

export function useUpdateProfile() {
  return useProfileMutation((input: SchoolProfileInput) => apiRequest<SchoolProfile>(profilePath, "PUT", input));
}

export function useUploadAsset(kind: AssetKind) {
  return useProfileMutation(({ file, version }: { file: File; version: number }) => {
    const form = new FormData();
    form.append("file", file);
    form.append("version", String(version));
    return apiRequest<SchoolProfile>(`${profilePath}/${kind}`, "POST", form);
  });
}

export function useRemoveAsset(kind: AssetKind) {
  return useProfileMutation((version: number) =>
    apiRequest<SchoolProfile>(`${profilePath}/${kind}?version=${version}`, "DELETE"));
}
