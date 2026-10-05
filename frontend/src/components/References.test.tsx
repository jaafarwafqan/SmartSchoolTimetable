import { describe, expect, it } from "vitest";
import { createFormatter } from "../lib/format";
import type { DependentGroup } from "../lib/referencesApi";
import { describeGroup } from "./References";

const format = createFormatter();

const sections: DependentGroup = { kind: "section", active: 2, archived: 1, samples: ["الأول / أ", "الأول / ب", "الأول / ج"], errorCode: "RECORD_IN_USE" };
const lines: DependentGroup = { kind: "curriculumEntry", active: 7, archived: 0, samples: ["الأول / العربية", "الأول / الرياضيات"], errorCode: "CURRICULUM_IN_USE" };

describe("describeGroup", () => {
  it("counts every dependent for delete and notes the archived ones", () => {
    expect(describeGroup(sections, "delete", format)).toBe("٣ شعب (الأول / أ، الأول / ب، الأول / ج)، منها ١ في الأرشيف");
  });

  it("counts only active dependents for archive", () => {
    expect(describeGroup(sections, "archive", format)).toBe("شعبتان (الأول / أ، الأول / ب)");
  });

  it("names curriculum lines and says how many names are not shown", () => {
    expect(describeGroup(lines, "delete", format)).toBe("٧ بنود في المنهج (الأول / العربية، الأول / الرياضيات و٥ غيرها)");
  });
});
