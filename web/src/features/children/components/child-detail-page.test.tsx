import { describe, it, expect, vi, beforeEach } from "vitest";
import { act, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
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
    gradeLevel: "G5",
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
  return render(
    <MemoryRouter initialEntries={["/children/1"]}>
      <ToastProvider>
        <Routes>
          <Route path="/children/:childId" element={<ChildDetailPage />} />
        </Routes>
      </ToastProvider>
    </MemoryRouter>
  );
}

describe("ChildDetailPage", () => {
  beforeEach(() => {
    childrenApi.getChild.mockReset();
    childrenApi.updateChild.mockReset();
    childrenApi.deleteChild.mockReset();
    childLinksApi.getChildSchoolLinks.mockReset();
    childLinksApi.getChildSchoolLinks.mockResolvedValue({ success: true, data: [] });
  });

  it("never puts the child's legal name in the tab title, even though the heading shows it", async () => {
    childrenApi.getChild.mockResolvedValue({ success: true, data: makeChild() });
    renderPage();

    expect(await screen.findByRole("heading", { level: 1, name: "Ada Lovelace" })).toBeInTheDocument();
    expect(document.title).toBe("Child profile · IEP Advisor");
  });

  it("keeps the non-identifying title while the child record is still loading", async () => {
    childrenApi.getChild.mockReturnValue(new Promise(() => {}));
    childLinksApi.getChildSchoolLinks.mockReturnValue(new Promise(() => {}));
    renderPage();

    expect(document.title).toBe("Child profile · IEP Advisor");
    // Let effects settle before the test tears the tree down.
    await act(async () => {});
  });
});
