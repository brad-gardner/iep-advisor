import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import type { User } from "@/types/api";

const useAuthMock = vi.fn();
vi.mock("@/features/auth/hooks/use-auth", () => ({
  useAuth: () => useAuthMock(),
}));

const useStudentWorkspaceMock = vi.fn();
vi.mock("../hooks/use-student-workspace", () => ({
  useStudentWorkspace: () => useStudentWorkspaceMock(),
}));

const useHomeMock = vi.fn();
vi.mock("@/features/home/hooks/use-home", () => ({
  useHome: () => useHomeMock(),
}));

const meetingsApi = vi.hoisted(() => ({ rsvpToMeeting: vi.fn() }));
vi.mock("@/features/meetings/api/meetings-api", () => meetingsApi);

import { StudentHomePage } from "./student-home-page";

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: "ada@example.com",
    firstName: "Ada",
    lastName: "Lovelace",
    state: "OH",
    role: "Student",
    fullName: "Ada Lovelace",
    onboardingCompleted: true,
    subscriptionStatus: "active",
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <StudentHomePage />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe("StudentHomePage", () => {
  beforeEach(() => {
    useAuthMock.mockReset();
    useStudentWorkspaceMock.mockReset();
    useHomeMock.mockReset();
    meetingsApi.rsvpToMeeting.mockReset();
    useAuthMock.mockReturnValue({ user: makeUser() });
    useStudentWorkspaceMock.mockReturnValue({
      entries: [],
      status: "loading",
      reload: vi.fn(),
      addEntry: vi.fn(),
      updateEntry: vi.fn(),
      setShareable: vi.fn(),
      removeEntry: vi.fn(),
      interview: vi.fn(),
    });
  });

  it("sets the tab title from the student's name", () => {
    useHomeMock.mockReturnValue({ home: null });
    renderPage();
    expect(document.title).toBe("Welcome, Ada · IEP Advisor");
  });

  it("shows the workspace nudge when the server provides one", () => {
    useHomeMock.mockReturnValue({
      home: {
        kind: "Student",
        generatedAt: "2026-09-16T00:00:00.000Z",
        student: {
          displayName: "Ada Lovelace",
          nextMeeting: null,
          workspaceNudge: "Add your thoughts before Friday's meeting",
          linkedStudentId: 10,
        },
      },
    });
    renderPage();

    expect(screen.getByTestId("student-home-nudge")).toHaveTextContent(
      "Add your thoughts before Friday's meeting"
    );
    expect(screen.queryByTestId("student-home-next-meeting")).not.toBeInTheDocument();
  });

  it("shows the next meeting and lets the student RSVP", async () => {
    const user = userEvent.setup();
    meetingsApi.rsvpToMeeting.mockResolvedValue({
      success: true,
      data: { myInviteStatus: "Accepted" },
    });
    useHomeMock.mockReturnValue({
      home: {
        kind: "Student",
        generatedAt: "2026-09-16T00:00:00.000Z",
        student: {
          displayName: "Ada Lovelace",
          workspaceNudge: null,
          linkedStudentId: 10,
          nextMeeting: {
            id: 77,
            title: "Annual review",
            type: "AnnualReview",
            startsAtUtc: "2099-01-01T15:00:00.000Z",
            timeZoneId: "America/New_York",
            durationMinutes: 60,
            studentId: 10,
            studentName: "Ada Lovelace",
            myInviteStatus: "Pending",
            status: "Scheduled",
          },
        },
      },
    });
    renderPage();

    expect(screen.getByTestId("student-home-next-meeting")).toBeInTheDocument();
    await user.click(screen.getByTestId("next-meeting-accept"));
    expect(meetingsApi.rsvpToMeeting).toHaveBeenCalledWith(77, { status: "Accepted" });
  });

  it("shows neither nudge nor meeting card when the server has nothing to say", () => {
    useHomeMock.mockReturnValue({
      home: {
        kind: "Student",
        generatedAt: "2026-09-16T00:00:00.000Z",
        student: { displayName: "Ada Lovelace", nextMeeting: null, workspaceNudge: null, linkedStudentId: 10 },
      },
    });
    renderPage();

    expect(screen.queryByTestId("student-home-nudge")).not.toBeInTheDocument();
    expect(screen.queryByTestId("student-home-next-meeting")).not.toBeInTheDocument();
  });
});
