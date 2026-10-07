import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

const childrenApi = vi.hoisted(() => ({ createChild: vi.fn() }));
vi.mock("../api/children-api", () => childrenApi);

const useChildren = vi.hoisted(() => vi.fn());
vi.mock("../hooks/use-children", () => ({ useChildren }));

import { ChildrenListPage } from "./children-list-page";

function renderPage() {
  return renderInSpanish(
    <MemoryRouter>
      <ToastProvider>
        <ChildrenListPage />
      </ToastProvider>
    </MemoryRouter>,
    { ns: "children" }
  );
}

describe("ChildrenListPage in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the empty state and primary action in Spanish", async () => {
    useChildren.mockReturnValue({ children: [], isLoading: false, reload: vi.fn() });
    await renderPage();

    expect(screen.getByRole("heading", { name: "Sus hijos" })).toBeInTheDocument();
    expect(screen.getByText("Aún no hay perfiles de hijos.")).toBeInTheDocument();
    expect(screen.getByTestId("add-first-child-button")).toHaveTextContent("Agregue a su primer hijo");
    expect(screen.getByTestId("add-child-button")).toHaveTextContent("Agregar hijo");
  });

  it("shows a child's grade and disability category labels translated", async () => {
    useChildren.mockReturnValue({
      children: [
        {
          id: 1,
          firstName: "Ada",
          lastName: "Lovelace",
          role: "owner",
          gradeLevel: "3rd",
          disabilityCategory: "Autism",
          schoolDistrict: null,
        },
      ],
      isLoading: false,
      reload: vi.fn(),
    });
    await renderPage();

    expect(screen.getByText("Grado: 3.er grado")).toBeInTheDocument();
    expect(screen.getByText("Autismo")).toBeInTheDocument();
  });
});
