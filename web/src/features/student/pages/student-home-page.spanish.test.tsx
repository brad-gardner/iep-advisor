import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ToastProvider } from "@/components/ui/toast";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";
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
    preferredLanguage: null,
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <MemoryRouter>
      <ToastProvider>
        <StudentHomePage />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe("StudentHomePage in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the welcome title and workspace sections in Spanish", async () => {
    useAuthMock.mockReturnValue({ user: makeUser() });
    useHomeMock.mockReturnValue({ home: null });
    useStudentWorkspaceMock.mockReturnValue({
      entries: [],
      status: "ready",
      reload: vi.fn(),
      addEntry: vi.fn(),
      updateEntry: vi.fn(),
      setShareable: vi.fn(),
      removeEntry: vi.fn(),
      interview: vi.fn(),
    });

    await renderPage();

    expect(document.title).toBe("Bienvenido, Ada · IEP Advisor");
    expect(screen.getByRole("heading", { name: "Mis fortalezas" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Lo que quiero decir en mi reunión" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Entrevista con IA" })).toBeInTheDocument();
  });

  it("renders the workspace load error in Spanish", async () => {
    useAuthMock.mockReturnValue({ user: makeUser() });
    useHomeMock.mockReturnValue({ home: null });
    useStudentWorkspaceMock.mockReturnValue({
      entries: [],
      status: "error",
      reload: vi.fn(),
      addEntry: vi.fn(),
      updateEntry: vi.fn(),
      setShareable: vi.fn(),
      removeEntry: vi.fn(),
      interview: vi.fn(),
    });

    await renderPage();

    expect(screen.getByText("No pudimos cargar su espacio")).toBeInTheDocument();
    expect(screen.getByText("Algo salió mal. Inténtelo de nuevo.")).toBeInTheDocument();
  });
});
