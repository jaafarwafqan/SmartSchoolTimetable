import type { WizardYearInput } from "./wizardApi";

/**
 * Proposed academic years for the wizard (spec 2.5 §5 step 2): the Iraqi year starts in September, so from July on
 * the proposal is this year–next year, before July last year–this year. Neighbouring years are offered too.
 * Dates and the two terms are suggestions the owner can edit.
 */
export function proposedStartYear(todayIso: string): number {
  const [year, month] = todayIso.split("-").map(Number);
  return month >= 7 ? year : year - 1;
}

export function proposeYear(startYear: number, termNames: readonly string[]): WizardYearInput {
  const next = startYear + 1;
  return {
    label: `${startYear}-${next}`,
    startDate: `${startYear}-09-01`,
    endDate: `${next}-06-30`,
    terms: [
      { name: termNames[0] ?? "", startDate: `${startYear}-09-01`, endDate: `${next}-01-15` },
      { name: termNames[1] ?? "", startDate: `${next}-02-01`, endDate: `${next}-06-30` },
    ],
  };
}

export function yearChoices(todayIso: string): number[] {
  const start = proposedStartYear(todayIso);
  return [start - 1, start, start + 1];
}
