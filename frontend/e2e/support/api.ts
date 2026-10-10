import { expect, type Page } from "@playwright/test";

/**
 * Calls the real API from the signed-in page's browser context (same cookies), with the per-launch token from
 * bootstrap and a loopback Origin, so test data can be prepared quickly through the normal endpoints.
 */
export async function api<T = unknown>(page: Page, baseUrl: string, method: "GET" | "POST" | "PUT" | "DELETE", path: string, body?: unknown): Promise<T> {
  const bootstrap = await page.request.get(`${baseUrl}/api/v1/bootstrap`);
  const { launchToken } = (await bootstrap.json()) as { launchToken: string };
  const response = await page.request.fetch(`${baseUrl}/api/v1${path}`, {
    method,
    data: body,
    headers: { "X-Local-Launch-Token": launchToken, Origin: baseUrl },
  });
  expect(response.ok(), `${method} ${path} -> ${response.status()} ${await response.text()}`).toBeTruthy();
  return (response.status() === 204 ? undefined : await response.json()) as T;
}

type Shift = { id: number; kind: string };
type Year = { id: number };

/** Builds a school through the wizard endpoints and templates: returns the current year id and its shifts. */
export async function prepareSchool(page: Page, baseUrl: string, options: { schoolType: string; shiftMode: string; lessons?: number; grades: { gradeKey: string; branches?: string[]; sections: number; shift?: "morning" | "evening" }[]; subjects?: string[] }) {
  await api(page, baseUrl, "PUT", "/setup-wizard/school", { name: "مدرسة الاختبار", schoolType: options.schoolType, principalName: null });
  await api(page, baseUrl, "PUT", "/setup-wizard/year", {
    label: "2026-2027", startDate: "2026-09-01", endDate: "2027-06-30",
    terms: [{ name: "الفصل الأول", startDate: "2026-09-01", endDate: "2027-01-15" }, { name: "الفصل الثاني", startDate: "2027-02-01", endDate: "2027-06-30" }],
  });
  // MF7: one shift in every system; «مزدوج» adds the evening session (same lessons) and the day mapping.
  const days = [7, 1, 2, 3, 4];
  const timing = (kind: "morning" | "evening") => ({ kind, firstStartTime: kind === "morning" ? "08:00" : "13:00", lessonMinutes: 45, lessonCount: options.lessons ?? 7, breaks: [{ afterLesson: 3, minutes: 15 }], dayLessons: [] });
  const system = options.shiftMode === "dual" ? "dual" : options.shiftMode === "evening" ? "evening" : "morning";
  await api(page, baseUrl, "PUT", "/setup-wizard/timing", {
    days, weekStartDay: 7, system, main: timing(system === "evening" ? "evening" : "morning"),
    ...(system === "dual"
      ? { evening: timing("evening"), sessionDays: [1, 2].flatMap((term) => days.map((day, index) => ({ term, day, session: (index < 2) === (term === 1) ? "morning" : "evening" }))) }
      : {}),
  });
  const years = await api<{ items: Year[] }>(page, baseUrl, "GET", "/academic-years/");
  const yearId = years.items[0].id;
  const shifts = (await api<{ items: Shift[] }>(page, baseUrl, "GET", `/academic-years/${yearId}/shifts/?pageSize=100`)).items;
  const shiftId = () => shifts[0].id;
  await api(page, baseUrl, "POST", `/academic-years/${yearId}/templates/stages`, {
    schoolType: options.schoolType,
    grades: options.grades.map((grade) => ({ gradeKey: grade.gradeKey, branches: grade.branches ?? [], sections: grade.sections, shiftId: shiftId(), labelStyle: "arabic" })),
  });
  if (options.subjects) await api(page, baseUrl, "POST", "/templates/subjects", { names: options.subjects });
  return { yearId, shifts };
}
