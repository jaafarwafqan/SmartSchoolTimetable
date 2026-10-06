import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { guideMessages } from "./guideMessages";
import { SchedulingSection } from "./SchedulingSection";

const text = guideMessages.scheduling;

// The style guide is a development-only route, so the production build used by Playwright cannot open it (D2).
describe("style guide: scheduling components", () => {
  it("renders the assignment matrix, the load bar and the readiness finding", () => {
    const client = new QueryClient({ defaultOptions: { queries: { enabled: false, retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter><SchedulingSection /></MemoryRouter>
      </QueryClientProvider>,
    );

    const card = screen.getByRole("heading", { name: text.title, level: 2 }).closest<HTMLElement>(".guide-section");
    expect(card).not.toBeNull();
    const section = card as HTMLElement;
    for (const heading of [text.matrix, text.loadTitle, text.readinessTitle]) {
      expect(within(section).getByRole("heading", { name: heading, level: 3 })).toBeVisible();
    }
    const table = within(section).getByRole("table");
    expect(within(table).getByText(text.teacher)).toBeVisible();
    expect(within(table).getByText(text.unassigned)).toBeVisible();
    expect(within(section).getByRole("meter", { name: text.loadLabel })).toBeVisible();
    expect(within(section).getByText(text.finding)).toBeVisible();
    expect(within(section).getByRole("link", { name: text.openReport })).toHaveAttribute("href", "/readiness");
  });
});
