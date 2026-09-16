import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import { ORG_ROLE, type EducatorProfile } from "../types";
import { makeStudent } from "../test/fixtures";

const useStudentRecordMock = vi.fn();
vi.mock("../hooks/use-student-record", () => ({
  useStudentRecord: () => useStudentRecordMock(),
}));

const useEducatorProfileMock = vi.fn();
vi.mock("../hooks/use-educator-profile", () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const districtApi = vi.hoisted(() => ({ getDistrictSchools: vi.fn() }));
vi.mock("@/features/district-admin/api/district-api", () => districtApi);

// The rest of the page's children are irrelevant to the tab-title fix under
// test — stub them out so the page renders without their own data fetching.
vi.mock("../components/family-links-section", () => ({
  FamilyLinksSection: () => <div data-testid="family-links-section" />,
}));
vi.mock("../components/student-details-card", () => ({
  StudentDetailsCard: () => <div data-testid="student-details-card" />,
}));
vi.mock("../components/student-documents-section", () => ({
  StudentDocumentsSection: () => <div data-testid="student-documents-section" />,
}));
vi.mock("../components/edit-student-drawer", () => ({
  EditStudentDrawer: () => null,
}));
vi.mock("../components/lifecycle/student-lifecycle-actions", () => ({
  StudentLifecycleActions: () => <div data-testid="student-lifecycle-actions" />,
}));
vi.mock("../components/team/student-team-panel", () => ({
  StudentTeamPanel: () => <div data-testid="student-team-panel" />,
}));
vi.mock("@/features/student/components/invite-student-form", () => ({
  InviteStudentForm: () => null,
}));
vi.mock("@/features/student/api/student-invite-api", () => ({
  inviteStudentFromEducator: vi.fn(),
}));
vi.mock("@/features/meetings/components/student-meetings-card", () => ({
  StudentMeetingsCard: () => <div data-testid="student-meetings-card" />,
}));
vi.mock("@/features/obligations/components/student-timeline-card", () => ({
  StudentTimelineCard: () => <div data-testid="student-timeline-card" />,
}));

import { EducatorStudentDetailPage } from "./educator-student-detail-page";

function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.SchoolAdmin,
    orgRoleName: "SchoolAdmin",
    districtId: 1,
    districtName: "Test District",
    schoolId: 5,
    schoolName: "Lincoln Elementary",
    isActive: true,
    stateCode: "OH",
    title: null,
    credentials: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <ToastProvider>
      <MemoryRouter initialEntries={["/educator/students/10"]}>
        <Routes>
          <Route path="/educator/students/:studentId" element={<EducatorStudentDetailPage />} />
        </Routes>
      </MemoryRouter>
    </ToastProvider>
  );
}

describe("EducatorStudentDetailPage", () => {
  beforeEach(() => {
    useStudentRecordMock.mockReset();
    useEducatorProfileMock.mockReset();
    districtApi.getDistrictSchools.mockReset();
    districtApi.getDistrictSchools.mockResolvedValue({ success: true, data: [] });
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile() });
  });

  it("never puts the student's legal name in the tab title, even though the heading shows it", () => {
    useStudentRecordMock.mockReturnValue({
      student: makeStudent({ firstName: "Ada", lastName: "Lovelace" }),
      isLoading: false,
      reload: vi.fn(),
      update: vi.fn(),
      exit: vi.fn(),
      reactivate: vi.fn(),
      archive: vi.fn(),
      transfer: vi.fn(),
    });
    renderPage();

    expect(document.title).toBe("Student record · IEP Advisor");
    // The in-page heading is unaffected — the name still shows there.
    expect(screen.getByRole("heading", { level: 1, name: "Ada Lovelace" })).toBeInTheDocument();
  });

  it("keeps the non-identifying title while the student record is still loading", () => {
    useStudentRecordMock.mockReturnValue({
      student: null,
      isLoading: true,
      reload: vi.fn(),
      update: vi.fn(),
      exit: vi.fn(),
      reactivate: vi.fn(),
      archive: vi.fn(),
      transfer: vi.fn(),
    });
    renderPage();

    expect(document.title).toBe("Student record · IEP Advisor");
  });
});
