import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";
import { ORG_ROLE, type EducatorProfile } from "../types";
import { makeStudent } from "../test/fixtures";
// `educator` is a staff-only namespace (plan phase 5) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the page's real lazy route chunk
// (`app/lazy-routes/staff-routes.tsx`) registers it before the page can
// render. Without this, `useTranslation('educator')` inside the page would
// try (and, correctly, fail loudly) to fetch it from the Spanish-only
// backend for English. `evaluation`, `obligations` and `family-contact` are
// all staff-only too, and needed here because this page renders the real
// (unmocked) `EvaluationCard` and `FamilyContactCard`, which name them in
// their own `useTranslation` calls (for `evaluationCaseStatusLabel`/etc.,
// `obligationKindLabel`/`ObligationStatusChip`, and
// `familyContactMethodLabel`/`familyContactOutcomeLabel`).
import '@/app/lazy-routes/staff-locales';
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

  // Proves the staff/admin namespace split end to end (plan phase 5), the
  // same way `educator-students-page.test.tsx` does: `educator`'s English is
  // registered above via the `@/app/lazy-routes/staff-locales` side-effect import, and its
  // Spanish still lazy-loads like any other namespace — `renderInSpanish`
  // needs the extra `ns: 'educator'` (`docs/i18n/README.md`'s "Namespace
  // coverage" / test conventions).
  describe("in Spanish", () => {
    afterEach(() => resetTestLanguage());

    it("renders the heading and sidebar actions in Spanish", async () => {
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

      await renderInSpanish(
        <ToastProvider>
          <MemoryRouter initialEntries={["/educator/students/10"]}>
            <Routes>
              <Route path="/educator/students/:studentId" element={<EducatorStudentDetailPage />} />
            </Routes>
          </MemoryRouter>
        </ToastProvider>,
        { ns: ['educator', 'evaluation', 'obligations', 'family-contact'] }
      );

      expect(screen.getByRole("heading", { level: 1, name: "Ada Lovelace" })).toBeInTheDocument();
      expect(screen.getByText("Equipo del IEP")).toBeInTheDocument();
      expect(screen.getByText("Exportar registro")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Invitar estudiante" })).toBeInTheDocument();
    });
  });
});
