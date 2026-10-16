import { QueryClient } from "@tanstack/react-query";

/**
 * `networkMode: "always"`: the program talks only to the local server (127.0.0.1), never to the internet. TanStack Query's default
 * («online») pauses every request while the browser reports «no network», so cutting the internet (or unplugging the cable) froze the
 * screens on a spinner although the local server was running. Both queries and mutations must ignore the browser's online state.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: false,
      networkMode: "always",
    },
    mutations: {
      networkMode: "always",
    },
  },
});
