import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiRequest } from "../../api";
import { refreshQueries } from "../../lib/refreshQueries";
import type { Paged } from "../academic-years/yearsApi";

export const resourceKinds = ["lab", "field", "hall", "other"] as const;
export type ResourceKind = (typeof resourceKinds)[number];

export type Resource = {
  id: number;
  name: string;
  kind: ResourceKind;
  capacity: number;
  notes: string | null;
  isArchived: boolean;
  archivedAt: string | null;
  version: number;
};
export type ResourceInput = { name: string; kind: ResourceKind; capacity: number; notes: string | null; version: number };

export const minCapacity = 1;
export const maxCapacity = 20;

const resourcesKey = ["resources"] as const;
const resourcesPath = "/api/v1/resources";

export function useResources(params: { search: string; page: number; pageSize: number; includeArchived: boolean }) {
  const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize), includeArchived: String(params.includeArchived) });
  if (params.search.trim()) query.set("search", params.search.trim());
  return useQuery({
    queryKey: [...resourcesKey, params],
    queryFn: () => apiRequest<Paged<Resource>>(`${resourcesPath}/?${query.toString()}`),
    placeholderData: keepPreviousData,
  });
}

/** Every resource (archived included) for the subject editor's chooser; a school has a handful. */
export function useAllResources() {
  return useQuery({
    queryKey: [...resourcesKey, "all"],
    queryFn: () => apiRequest<Paged<Resource>>(`${resourcesPath}/?pageSize=100&includeArchived=true`),
  });
}

function useResourcesMutation<TInput, TResult>(request: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: request,
    onSuccess: async () => { await refreshQueries(queryClient, [resourcesKey, ["references"]]); },
  });
}

export function useSaveResource() {
  return useResourcesMutation(({ id, input }: { id: number | null; input: ResourceInput }) =>
    id === null
      ? apiRequest<Resource>(`${resourcesPath}/`, "POST", input)
      : apiRequest<Resource>(`${resourcesPath}/${id}`, "PUT", input));
}

export function useResourceAction() {
  return useResourcesMutation(({ resource, action }: { resource: Resource; action: "archive" | "restore" | "delete" }) =>
    action === "delete"
      ? apiRequest<unknown>(`${resourcesPath}/${resource.id}?version=${resource.version}`, "DELETE")
      : apiRequest<Resource>(`${resourcesPath}/${resource.id}/${action}`, "POST", { version: resource.version }));
}
