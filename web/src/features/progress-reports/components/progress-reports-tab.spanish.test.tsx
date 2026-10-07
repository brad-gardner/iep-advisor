import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { ToastProvider } from "@/components/ui/toast/toast-provider";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";

const api = vi.hoisted(() => ({ listByIep: vi.fn() }));
vi.mock("../api/progress-reports-api", () => api);

import { ProgressReportsTab } from "./progress-reports-tab";

describe("ProgressReportsTab in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the heading, body and primary action in Spanish", async () => {
    api.listByIep.mockResolvedValue({ success: true, data: [] });

    await renderInSpanish(
      <ToastProvider>
        <ProgressReportsTab iepId={1} childId={4} canEdit />
      </ToastProvider>,
      { ns: ["progress-reports", "iep-documents"] },
    );

    expect(await screen.findByRole("heading", { name: "Informes de progreso" })).toBeInTheDocument();
    expect(
      screen.getByText("Informes emitidos por la escuela que registran el progreso respecto a las metas de este IEP."),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Nuevo informe" })).toBeInTheDocument();
    expect(screen.getByText("Aún no hay informes de progreso para este IEP.")).toBeInTheDocument();
  });
});
