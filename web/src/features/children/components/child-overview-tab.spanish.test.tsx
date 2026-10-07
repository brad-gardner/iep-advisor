import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

vi.mock("@/features/sharing/components/share-child-dialog", () => ({ ShareChildDialog: () => null }));
vi.mock("@/features/sharing/components/access-list", () => ({ AccessList: () => null }));
vi.mock("@/features/iep-versions/components/school-ieps-card", () => ({ SchoolIepsCard: () => null }));
vi.mock("@/features/contributions/components/about-my-child-card", () => ({ AboutMyChildCard: () => null }));
vi.mock("@/features/journal/components/journal-card", () => ({ JournalCard: () => null }));
vi.mock("@/features/shared-drafts/components/shared-drafts-card", () => ({ SharedDraftsCard: () => null }));
vi.mock("@/features/goals/components/child-progress-card", () => ({ ChildProgressCard: () => null }));
vi.mock("@/features/student/components/invite-student-form", () => ({ InviteStudentForm: () => null }));
vi.mock("./upcoming-meeting-card", () => ({ UpcomingMeetingCard: () => null }));

const useOutletContext = vi.hoisted(() => vi.fn());
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useOutletContext };
});

import { ChildOverviewTab } from "./child-overview-tab";

describe("ChildOverviewTab in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the profile, sharing and student-account headings in Spanish", async () => {
    useOutletContext.mockReturnValue({
      child: { id: 1, firstName: "Ada", role: "owner", gradeLevel: "5th", disabilityCategory: "Autism", schoolDistrict: null, dateOfBirth: null },
      childId: 1,
    });

    await renderInSpanish(
      <MemoryRouter>
        <ChildOverviewTab />
      </MemoryRouter>,
      { ns: "children" }
    );

    expect(screen.getByRole("heading", { name: "Perfil" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Acceso compartido" })).toBeInTheDocument();
    expect(screen.getByTestId("share-invite-button")).toHaveTextContent("Invitar a alguien");
    expect(screen.getByRole("heading", { name: "Cuenta del estudiante" })).toBeInTheDocument();
    expect(screen.getByTestId("invite-student-open")).toHaveTextContent("Invitar al estudiante");
    expect(screen.getByText("Grado")).toBeInTheDocument();
    expect(screen.getByText("5.º grado")).toBeInTheDocument();
    expect(screen.getByText("Autismo")).toBeInTheDocument();
  });
});
