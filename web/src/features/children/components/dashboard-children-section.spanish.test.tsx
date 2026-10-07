import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

const useChildren = vi.hoisted(() => vi.fn());
vi.mock("@/features/children/hooks/use-children", () => ({ useChildren }));

import { DashboardChildrenSection } from "./dashboard-children-section";

describe("DashboardChildrenSection in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the empty state in Spanish", async () => {
    useChildren.mockReturnValue({ children: [], isLoading: false, error: null, reload: vi.fn() });

    await renderInSpanish(
      <MemoryRouter>
        <DashboardChildrenSection />
      </MemoryRouter>,
      { ns: "children" }
    );

    expect(screen.getByRole("heading", { name: "Mis hijos" })).toBeInTheDocument();
    expect(screen.getByText("Aún no hay perfiles de hijos")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Agregue el perfil de su primer hijo" })).toBeInTheDocument();
  });
});
