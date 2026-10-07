import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

const useAdvocacyGoals = vi.hoisted(() => vi.fn());
vi.mock("@/features/advocacy-goals/hooks/use-advocacy-goals", () => ({ useAdvocacyGoals }));
vi.mock("@/features/advocacy-goals/components/advocacy-goals-list", () => ({ AdvocacyGoalsList: () => null }));

const useOutletContext = vi.hoisted(() => vi.fn());
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useOutletContext };
});

import { ChildGoalsTab } from "./child-goals-tab";

describe("ChildGoalsTab in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the heading in Spanish", async () => {
    useOutletContext.mockReturnValue({ child: { id: 1, firstName: "Ada", role: "owner" }, childId: 1 });
    useAdvocacyGoals.mockReturnValue({ goals: [], isLoading: false, reload: vi.fn() });

    await renderInSpanish(
      <MemoryRouter>
        <ChildGoalsTab />
      </MemoryRouter>,
      { ns: "children" }
    );

    expect(screen.getByRole("heading", { name: "Sus metas de defensa" })).toBeInTheDocument();
  });
});
