import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";
import type { ChildProfile } from "@/types/api";

const childrenApi = vi.hoisted(() => ({
  getChild: vi.fn(),
  updateChild: vi.fn(),
  deleteChild: vi.fn(),
}));
vi.mock("../api/children-api", () => childrenApi);

const childLinksApi = vi.hoisted(() => ({ getChildSchoolLinks: vi.fn() }));
vi.mock("@/features/child-links/api/child-links-api", () => childLinksApi);

vi.mock("./child-form", () => ({ ChildForm: () => null }));
vi.mock("@/features/sharing/components/shared-badge", () => ({
  SharedBadge: () => <div data-testid="shared-badge" />,
}));
vi.mock("@/features/child-links/components/school-link-badge", () => ({
  SchoolLinkBadge: () => <div data-testid="school-link-badge" />,
}));

import { ChildDetailPage } from "./child-detail-page";

function makeChild(overrides: Partial<ChildProfile> = {}): ChildProfile {
  return {
    id: 1,
    firstName: "Ada",
    lastName: "Lovelace",
    dateOfBirth: "2015-03-04",
    gradeLevel: "5th",
    disabilityCategory: null,
    schoolDistrict: null,
    role: "owner",
    currentIepDocumentId: null,
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <MemoryRouter initialEntries={["/children/1"]}>
      <ToastProvider>
        <Routes>
          <Route path="/children/:childId" element={<ChildDetailPage />} />
        </Routes>
      </ToastProvider>
    </MemoryRouter>,
    { ns: "children" }
  );
}

describe("ChildDetailPage in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the actions, tab labels and dialogs in Spanish", async () => {
    childrenApi.getChild.mockResolvedValue({ success: true, data: makeChild() });
    childLinksApi.getChildSchoolLinks.mockResolvedValue({ success: true, data: [] });
    await renderPage();

    expect(await screen.findByRole("heading", { level: 1, name: "Ada Lovelace" })).toBeInTheDocument();
    expect(screen.getByTestId("child-edit-button")).toHaveTextContent("Editar");
    expect(screen.getByTestId("child-remove-button")).toHaveTextContent("Eliminar");
    expect(screen.getByTestId("tab-overview")).toHaveTextContent("Resumen");
    expect(screen.getByTestId("tab-goals")).toHaveTextContent("Metas");
    expect(screen.getByTestId("tab-etrs")).toHaveTextContent("ETR");
  });
});
