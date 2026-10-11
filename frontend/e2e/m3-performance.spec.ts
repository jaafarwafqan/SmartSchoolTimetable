import { expect, test } from "@playwright/test";
import { writeFileSync } from "node:fs";
import { api, prepareSchool } from "./support/api";
import { ApiServer } from "./support/apiServer";
import { limitCores, sampleMemory } from "./support/processMemory";
import { setupOwner } from "./support/flows";

// M3 performance and memory measurement (docs/PERFORMANCE.md). On demand only: SST_PERF=1 (and SST_RELEASE_EXE for the published folder).
//   SST_PERF_SECTIONS="24,40"    school sizes to measure
//   SST_PERF_CORES=2             limit the app to this many processors (a school PC)
//   DOTNET_GCHeapHardLimit=0x20000000   set before starting: a 512 MB managed-heap limit (the app then also sees a small machine)
// A synthetic fictional intermediate school is entered through the normal endpoints; nothing is shown from a real school.
const sizes = (process.env.SST_PERF_SECTIONS ?? "24").split(",").map(Number);
const cores = Number(process.env.SST_PERF_CORES ?? 0);

type Subject = { id: number; name: string };
type Paged<T> = { items: T[] };
type Run = { id: number; status: string; workers: number; firstSolutionSeconds: number | null; elapsedSeconds: number | null; lessonsPlaced: number; objective: number | null; bound: number | null };

const specialists: Array<[string, string[]]> = [
  ["اللغة العربية", ["اللغة العربية"]], ["اللغة الإنكليزية", ["اللغة الإنكليزية"]], ["الرياضيات", ["الرياضيات"]],
  ["العلوم", ["الكيمياء", "الفيزياء", "الأحياء"]], ["الاجتماعيات", ["الاجتماعيات", "التربية الإسلامية"]],
  ["الأنشطة", ["الحاسوب", "التربية الأخلاقية", "التربية الفنية والنشيد", "التربية الرياضية"]],
];

test.skip(process.env.SST_PERF !== "1", "On-demand measurement: set SST_PERF=1.");

for (const sections of sizes) {
  test(`(M3) ${sections} sections: cold start, memory of the app and of the browser during a generation`, async ({ browser, page }, testInfo) => {
    test.setTimeout(900_000);
    const server = new ApiServer();
    const started = Date.now();
    await server.start(browser, `perf-${sections}`);
    const coldStartSeconds = (Date.now() - started) / 1000;
    const serverPid = server.process?.pid ?? 0;
    if (cores > 0) limitCores(serverPid, cores);
    try {
      await setupOwner(page, server.baseUrl, "owner", "Perf-Owner-1");
      const gradeSections = [Math.ceil(sections / 3), Math.ceil((sections - Math.ceil(sections / 3)) / 2)];
      gradeSections.push(sections - gradeSections[0] - gradeSections[1]);
      const { yearId } = await prepareSchool(page, server.baseUrl, {
        schoolType: "intermediate", shiftMode: "morning", grades: ["intermediate-1", "intermediate-2", "intermediate-3"].map((gradeKey, index) => ({ gradeKey, sections: gradeSections[index] })),
      });
      await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/curriculum/suggested`, { optionalSubjects: ["الحاسوب"], confirm: true });
      const subjects = new Map((await api<Paged<Subject>>(page, server.baseUrl, "GET", "/subjects/?pageSize=100")).items.map((subject) => [subject.name, subject.id]));
      // About 1.5 teachers per section, spread over the specialist groups (each teacher keeps the weekly limit).
      const teacherCount = Math.ceil(sections * 1.6);
      for (let index = 0; index < teacherCount; index++) {
        const [group, names] = specialists[index % specialists.length];
        await api(page, server.baseUrl, "POST", "/teachers/", {
          fullName: `معلم ${group} رقم ${index + 1}`, shortName: null, offDays: [], blockedPeriods: [], fullyReleased: false, releaseReason: null, releaseFrom: null, releaseTo: null,
          maxLessonsPerDay: null, maxLessonsPerWeek: null, notes: null, version: 0, specializationIds: names.map((name) => subjects.get(name)!).filter(Boolean),
        });
      }
      await api(page, server.baseUrl, "POST", `/academic-years/${yearId}/workload/suggestions/apply`, { confirm: true });
      const readiness = await api<{ ready: boolean; lines: number; assigned: number }>(page, server.baseUrl, "GET", `/academic-years/${yearId}/readiness/`);
      console.log(`M3 ${sections} sections: ${teacherCount} teachers, ${readiness.assigned}/${readiness.lines} lines assigned, ready=${readiness.ready}`);

      // The browser shows the generation screen while it runs (so «UI» memory is the real page, not an idle tab).
      await page.goto(`${server.baseUrl}/timetable/generate`);
      const before = sampleMemory(serverPid);
      let peakOwn = before.ownMb;
      let peakTree = 0;
      const nodeTree = () => sampleMemory(process.pid).treeMb; // the test runner, the app and the browser
      const run = await api<Run>(page, server.baseUrl, "POST", `/academic-years/${yearId}/generation/runs`, { timeLimitSeconds: 60 });
      let state = run;
      for (let attempt = 0; attempt < 400 && !["completed", "failed", "cancelled", "interrupted"].includes(state.status); attempt++) {
        await page.waitForTimeout(1000);
        state = await api<Run>(page, server.baseUrl, "GET", `/generation/runs/${run.id}`);
        peakOwn = Math.max(peakOwn, sampleMemory(serverPid).ownMb);
        if (attempt % 5 === 0) peakTree = Math.max(peakTree, nodeTree());
      }
      const line = {
        sections, teachers: teacherCount, workers: state.workers, cores: cores || "all", status: state.status, coldStartSeconds: Number(coldStartSeconds.toFixed(2)),
        firstSolutionSeconds: state.firstSolutionSeconds, elapsedSeconds: state.elapsedSeconds, lessonsPlaced: state.lessonsPlaced, objective: state.objective, bound: state.bound,
        appBeforeMb: Math.round(before.ownMb), appPeakMb: Math.round(peakOwn), allProcessesPeakMb: Math.round(peakTree),
      };
      console.log(`M3 RESULT ${JSON.stringify(line)}`);
      writeFileSync(testInfo.outputPath(`perf-${sections}.json`), JSON.stringify(line, null, 2));
      expect(["completed"]).toContain(state.status);
      expect(state.lessonsPlaced).toBeGreaterThan(0);
    } finally {
      await server.stop();
    }
  });
}
